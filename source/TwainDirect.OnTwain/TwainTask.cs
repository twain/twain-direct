///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirectOnTwain.TwainTask
//
//  A data structure that contains that SWORD task data converted into commands
//  for a TWAIN driver.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    29-Jun-2014     Splitting up the files
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2016 Kodak Alaris Inc.
//
//  Permission is hereby granted, free of charge, to any person obtaining a
//  copy of this software and associated documentation files (the "Software"),
//  to deal in the Software without restriction, including without limitation
//  the rights to use, copy, modify, merge, publish, distribute, sublicense,
//  and/or sell copies of the Software, and to permit persons to whom the
//  Software is furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
//  THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
//  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
//  DEALINGS IN THE SOFTWARE.
///////////////////////////////////////////////////////////////////////////////////////

// Helpers...
// Note that we are "cheating" by appealing directly to the TWAIN namespace.
// Most of the work is done through the toolkit.
using System;
using TWAINWorkingGroup;
using TWAINWorkingGroupToolkit;

namespace TwainDirectOnTwain
{
    /// <summary>
    /// A SWORD Task is converted to a TWAIN Task, to make it easier to manage
    /// streams, sources, formats and attributes within the context of a TWAIN
    /// driver.  This also provides vendors with sample code showing how they
    /// can stage SWORD values for copying into their device's internal data
    /// structures.
    /// </summary>
    #region TwainTask

    /// <summary>
    /// A TWAIN task consists of one or more actions, which will be run in
    /// turn.  We currently have a "configure" action and a "scan" action.
    /// 
    /// For the "configure" action we can have a list of streams.  We're going
    /// to use only one of those streams based on two criteria:
    /// 
    /// - the ability of the scanner to support the requested topology, for
    ///   instance, if we don't have a feeder or if the feeder is empty, then
    ///   we could be asked to try for a flatbed
    ///   
    /// - the attributes of the scanner topology, if a given attribute isn't
    ///   supported the task can request that we attempt to continue with the
    ///   next stream
    ///   
    /// The caller has the option for us to scan from a default stream if all
    /// of the explicit ones fail, or if they don't supply any streams.
    /// </summary>
    public sealed class TwainTask
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Extract data from the SWORD task and use to construct a data
        /// structure that's more in line with what we need to control TWAIN.
        /// There should be some parallels between doing this and doing what
        /// it takes to stage the data for setting up the internal control
        /// elements within a scanner.
        /// </summary>
        /// <param name="a_swordtask">The task to analyze</param>
         /// <returns></returns>
        public TwainTask(SwordTask a_swordtask)
        {
            bool blNextStream;
            Task task;

            // We need a valid sword object...
            if (a_swordtask == null)
            {
                throw new Exception("bad argument");
            }

            // We need a valid task...
            task = a_swordtask.GetTask();
            if (task == null)
            {
                a_swordtask.SetTaskError("nullTask", "", "", -1);
                throw new Exception("bad argument");
            }

            // If we have a null task this isn't an error, if we have some other
            // kind of content, then we need to complain...
            if (    (task.m_aswordaction == null)
                ||  (task.m_aswordaction[0] == null))
            {
                a_swordtask.SetTaskError("nullTask", "", "", -1);
                throw new Exception("null task");
            }

            // At this point we have a valid SWORD task...

            // Go through the actions...
            int iSwordAction;
            SwordAction[] aswordaction = task.m_aswordaction;
            for (iSwordAction = 0; iSwordAction < aswordaction.Length; iSwordAction++)
            {
                // Get an action...
                SwordAction swordaction = aswordaction[iSwordAction];
                TwainAction twainaction;

                // Anticipate success...
                swordaction.m_swordstatus = SwordStatus.SuccessIgnore;

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordaction.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific action...");
                    continue;
                }

                // Dispatch on the actions...
                switch (swordaction.m_szAction)
                {
                    // Ignore unrecognized commands, unless the exception tells us
                    // otherwise...
                    default:
                        if (swordaction.m_szException == "fail")
                        {
                            a_swordtask.SetTaskError("unsupported", swordaction.m_szJsonKey + ".action", swordaction.m_szAction, -1);
                            swordaction.m_swordstatus = SwordStatus.Unsupported;
                            throw new Exception("action failure");
                        }
                        break;

                    // Scan...
                    case "scan":
                        twainaction = AddAction(swordaction);
                        twainaction.m_szAction = swordaction.m_szAction;
                        swordaction.m_swordstatus = SwordStatus.Success;
                        break;

                    // This needs to move into a function...
                    case "configure": {
                        // Add our action...
                        twainaction = AddAction(swordaction);
                        twainaction.m_szAction = swordaction.m_szAction;

                        // If we don't have any streams, then we're using the defaults...
                        SwordStream[] aswordstream = aswordaction[iSwordAction].m_aswordstream;
                        if (    (aswordstream == null)
                            ||  (aswordstream.Length == 0)
                            ||  (aswordstream[0] == null))
                        {
                            // TBD: Reset the scanner...
                            swordaction.m_swordstatus = SwordStatus.SuccessIgnore;
                            continue;
                        }

                        // If we don't have any sources, then we're using the defaults...
                        SwordSource[] aswordsource = aswordstream[0].m_aswordsource;
                        if (    (aswordsource == null)
                            ||  (aswordsource.Length == 0)
                            ||  (aswordsource[0] == null))
                        {
                            // TBD: Reset the scanner...
                            swordaction.m_swordstatus = SwordStatus.SuccessIgnore;
                            continue;
                        }

                        // Go through the stream list...
                        int iSwordStream;
                        for (iSwordStream = 0; iSwordStream < aswordstream.Length; iSwordStream++)
                        {
                            // Quick lookup for the SWORD stream...
                            blNextStream = false;
                            SwordStream swordstream = aswordstream[iSwordStream];

                            // Vendor check, skip stuff that we don't recognize...
                            if (a_swordtask.GetGuidOwner(swordstream.m_szVendor) == SwordTask.GuidOwner.Unknown)
                            {
                                TWAINWorkingGroup.Log.Info("Skipping vendor specific stream...");
                                continue;
                            }

                            // Okay, TWAIN can have a stream now...
                            TwainStream twainstream = twainaction.AddStream(swordstream);

                            // Go through the source list...
                            int iSwordSource;
                            for (iSwordSource = 0; !blNextStream && (iSwordSource < swordstream.m_aswordsource.Length); iSwordSource++)
                            {
                                // Drill into the source...
                                if (swordstream.m_aswordsource[iSwordSource] != null)
                                {
                                    // Quick lookup for the SWORD source...
                                    SwordSource swordsource = swordstream.m_aswordsource[iSwordSource];

                                    // Vendor check, skip stuff that we don't recognize...
                                    if (a_swordtask.GetGuidOwner(swordsource.m_szVendor) == SwordTask.GuidOwner.Unknown)
                                    {
                                        TWAINWorkingGroup.Log.Info("Skipping vendor specific source...");
                                        continue;
                                    }

                                    // Add a TWAIN source...
                                    TwainSource twainsource = twainstream.AddSource(swordsource);

                                    // Handle the source address...
                                    switch (swordsource.m_szSource)
                                    {
                                        // We don't recognize this, so follow our exception...
                                        default:
                                            // Handle the exception right here...
                                            if (swordsource.m_szException == "fail")
                                            {
                                                TWAINWorkingGroup.Log.Info("Unrecognized source: " + swordsource.m_szSource);
                                                a_swordtask.SetTaskError(swordsource.m_szException, swordsource.m_szJsonKey + ".source", swordsource.m_szSource, -1);
                                                swordaction.m_swordstatus = SwordStatus.Unsupported;
                                                throw new Exception("action failure");
                                            }
                                            else if (swordsource.m_szException == "nextStream")
                                            {
                                                TWAINWorkingGroup.Log.Info("Unrecognized source: " + swordsource.m_szSource);
                                                blNextStream = true;
                                            }
                                            // How's this for sneaky?  We need a value here, we'll let the code try
                                            // to set it and fail.  Is there a better way of doing this?  Would I be
                                            // asking if there wasn't?
                                            twainsource.SetSource
                                            (
                                                "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_UINT16,-999",
                                                "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,-999",
                                                "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,-999",
                                                swordsource.m_szSource
                                            );
                                            break;

                                        // Arrange for feeder/flatbed (or anything)...
                                        case "any":
                                            twainsource.SetSource
                                            (
                                                "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_UINT16,1",
                                                "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,1",
                                                "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1",
                                                swordsource.m_szSource
                                            );
                                            break;

                                        // Arrange for a duplex feeder...
                                        case "feeder":
                                            twainsource.SetSource
                                            (
                                                "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_UINT16,0",
                                                "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,1",
                                                "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1",
                                                swordsource.m_szSource
                                            );
                                            break;

                                        // Arrange for a simplex feeder (front)...
                                        case "feederfront":
                                            twainsource.SetSource
                                            (
                                                "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_UINT16,0",
                                                "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,0",
                                                "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1",
                                                swordsource.m_szSource
                                            );
                                            break;

                                        // Arrange for a simplex feeder (rear)...
                                        // TBD: we'll need DAT_FILESYSTEM to do this one.
                                        //case "feederrear":
                                        //    break;

                                        // Arrange for a flatbed...
                                        case "flatbed":
                                            twainsource.SetSource
                                            (
                                                "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_UINT16,0",
                                                "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,0",
                                                "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,0",
                                                swordsource.m_szSource
                                            );
                                            break;
                                    }

                                    // We can have multiple formats in a source, in which case the scanner
                                    // will automatically pick the best match...
                                    if (    (swordsource.m_aswordpixelformat != null)
                                        &&  (swordsource.m_aswordpixelformat.Length > 0))
                                    {
                                        int iSwordPixelFormat;
                                        for (iSwordPixelFormat = 0; !blNextStream && (iSwordPixelFormat < swordsource.m_aswordpixelformat.Length); iSwordPixelFormat++)
                                        {
                                            // Quick lookup for the SWORD pixelType...
                                            SwordPixelFormat swordpixelformat = swordsource.m_aswordpixelformat[iSwordPixelFormat];

                                            // Vendor check, skip stuff that we don't recognize...
                                            if (a_swordtask.GetGuidOwner(swordpixelformat.m_szVendor) == SwordTask.GuidOwner.Unknown)
                                            {
                                                TWAINWorkingGroup.Log.Info("Skipping vendor specific pixelFormat...");
                                                continue;
                                            }

                                            // Okay, it's safe to make one of these now...
                                            TwainPixelFormat twainpixelformat = twainsource.AddPixelFormat(swordpixelformat);

                                            // Handle a pixelFormat...
                                            switch (swordpixelformat.m_szPixelFormat)
                                            {
                                                default:
                                                    // Handle the exception right here...
                                                    if (swordpixelformat.m_szException == "fail")
                                                    {
                                                        TWAINWorkingGroup.Log.Info("Unrecognized pixelFormat: " + swordpixelformat.m_szPixelFormat);
                                                        a_swordtask.SetTaskError(swordpixelformat.m_szException, swordpixelformat.m_szJsonKey + ".pixelFormat", swordpixelformat.m_szPixelFormat, -1);
                                                        swordaction.m_swordstatus = SwordStatus.Unsupported;
                                                        throw new Exception("action failure");
                                                    }
                                                    else if (swordpixelformat.m_szException == "nextStream")
                                                    {
                                                        TWAINWorkingGroup.Log.Info("Unrecognized pixelFormat: " + swordpixelformat.m_szPixelFormat);
                                                        blNextStream = true;
                                                    }
                                                    // Me again...
                                                    // How's this for sneaky?  We need a value here, we'll let the code try
                                                    // to set it and fail.  Is there a better way of doing this?  Would I be
                                                    // asking if there wasn't?
                                                    twainpixelformat.m_capabilityPixeltype = new Capability("ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,-999", swordpixelformat, twainpixelformat);
                                                    break;
                                                case "bw1":
                                                    twainpixelformat.m_capabilityPixeltype = new Capability("ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,0", swordpixelformat, twainpixelformat);
                                                    break;
                                                case "gray8":
                                                    twainpixelformat.m_capabilityPixeltype = new Capability("ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,1", swordpixelformat, twainpixelformat);
                                                    break;
                                                case "rgb24":
                                                    twainpixelformat.m_capabilityPixeltype = new Capability("ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,2", swordpixelformat, twainpixelformat);
                                                    break;
                                            }

                                            // Analyze the SWORD attributelist...
                                            if (swordpixelformat.m_aswordattribute != null)
                                            {
                                                int iSwordAttribute;
                                                for (iSwordAttribute = 0; !blNextStream && (iSwordAttribute < swordpixelformat.m_aswordattribute.Length); iSwordAttribute++)
                                                {
                                                    // Drill into the attribute...
                                                    if (swordpixelformat.m_aswordattribute[iSwordAttribute] != null)
                                                    {
                                                        // We're keeping this part flat.  We could have an attribute
                                                        // list for TWAIN, but at the moment that's adding complexity
                                                        // that doesn't have any clear value...
                                                        SwordAttribute swordattribute = swordpixelformat.m_aswordattribute[iSwordAttribute];

                                                        // Vendor check, skip stuff that we don't recognize...
                                                        if (a_swordtask.GetGuidOwner(swordattribute.m_szVendor) == SwordTask.GuidOwner.Unknown)
                                                        {
                                                            TWAINWorkingGroup.Log.Info("Skipping vendor specific attribute...");
                                                            continue;
                                                        }

                                                        // Dispatch...
                                                        switch (swordattribute.m_szAttribute)
                                                        {
                                                            // Handle stuff that confuddles us...
                                                            default:
                                                                TWAINWorkingGroup.Log.Info("Unrecognized attribute: " + swordattribute.m_szAttribute);
                                                                if (swordattribute.m_szException == "fail")
                                                                {
                                                                    a_swordtask.SetTaskError(swordattribute.m_szException, swordattribute.m_szJsonKey + ".attribute", swordattribute.m_szAttribute, -1);
                                                                    swordaction.m_swordstatus = SwordStatus.Unsupported;
                                                                    throw new Exception("action failure");
                                                                }
                                                                else if (swordattribute.m_szException == "nextStream")
                                                                {
                                                                    TWAINWorkingGroup.Log.Info("Unrecognized attribute: " + swordattribute.m_szAttribute);
                                                                    blNextStream = true;
                                                                }
                                                                break;

                                                            // Handle stuff we recognize...
                                                            case "autocrop":        SetAutoCrop(ref a_swordtask, ref twainpixelformat, ref swordattribute);  break;
                                                            case "autodeskew":      SetAutoDeskew(ref a_swordtask, ref twainpixelformat, ref swordattribute); break;
                                                            case "brightness":      SetBrightness(ref a_swordtask, ref twainpixelformat, ref swordattribute); break;
                                                            case "compression":     SetCompression(ref a_swordtask, ref twainpixelformat, ref swordattribute); break;
                                                            case "contrast":        SetContrast(ref a_swordtask, ref twainpixelformat, ref swordattribute); break;
                                                            case "imagecount":      SetImageCount(ref a_swordtask, ref twainpixelformat, ref swordattribute); break;
                                                            //case "crop":
                                                            //case "doublefeed":
                                                            case "resolution":      SetResolution(ref a_swordtask, ref twainpixelformat, ref swordattribute); break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }

            // All done...
            return;
        }

        /// <summary>
        /// Convert a string to a GUID...
        /// </summary>
        /// <param name="a_szGuid">string to convert</param>
        /// <returns>guid or empty</returns>
        public static Guid ConvertStringToGuid(string a_szGuid)
        {
            // Validate...
            if (string.IsNullOrEmpty(a_szGuid))
            {
                return (Guid.Empty);
            }

            // Convert...
            try
            {
                return (new Guid(a_szGuid));
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("Bad guid..." + a_szGuid);
                return (Guid.Empty);
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Add an action to this task...
        /// </summary>
        /// <returns></returns>
        private TwainAction AddAction(SwordAction a_swordaction)
        {
            // Add a TWAIN action...
            if (m_twainaction == null)
            {
                m_twainaction = new TwainAction[1];
            }
            else
            {
                TwainAction[] atwainaction = new TwainAction[m_twainaction.Length + 1];
                m_twainaction.CopyTo(atwainaction, 0);
                m_twainaction = atwainaction;
            }
            m_twainaction[m_twainaction.Length - 1] = new TwainAction(a_swordaction);
            TwainAction twainaction = m_twainaction[m_twainaction.Length - 1];

            // Copy the index, exception and the vendor...
            twainaction.m_szJsonKey = a_swordaction.m_szJsonKey;
            twainaction.m_szException = a_swordaction.m_szException;
            twainaction.m_guidVendor = ConvertStringToGuid(a_swordaction.m_szVendor);

            // All done...
            return (m_twainaction[m_twainaction.Length - 1]);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Capability Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Capability Attributes...

        /// <summary>
        /// Set the auto cropping...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetAutoCrop(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            bool blFirst = true;
            int iSwordValue;
            string szValue;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                switch (swordvalue.m_szValue)
                {
                    default:
                        // We're sort of hoping this will be a bad value...
                        szValue = "ICAP_AUTOMATICBORDETECTION,TWON_ONEVALUE,TWTY_BOOL,-9999";
                        break;
                    case "no":
                        szValue = "ICAP_AUTOMATICBORDETECTION,TWON_ONEVALUE,TWTY_BOOL,0";
                        break;
                    case "yes":
                        szValue = "ICAP_AUTOMATICBORDETECTION,TWON_ONEVALUE,TWTY_BOOL,1";
                        break;
                }

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityAutodeskew = new Capability(szValue, "autocrop", swordvalue);
                }
                else
                {
                    a_twainpixelformat.m_capabilityAutodeskew.AddValue(szValue, swordvalue);
                }
            }
        }

        /// <summary>
        /// Set the auto cropping...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetAutoDeskew(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            bool blFirst = true;
            int iSwordValue;
            string szValue;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                switch (swordvalue.m_szValue)
                {
                    default:
                        // We're sort of hoping this will be a bad value...
                        szValue = "ICAP_AUTOMATICDESKEW,TWON_ONEVALUE,TWTY_BOOL,-9999";
                        break;
                    case "no":
                        szValue = "ICAP_AUTOMATICDESKEW,TWON_ONEVALUE,TWTY_BOOL,0";
                        break;
                    case "yes":
                        szValue = "ICAP_AUTOMATICDESKEW,TWON_ONEVALUE,TWTY_BOOL,1";
                        break;
                }

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityAutodeskew = new Capability(szValue, "autodeskew", swordvalue);
                }
                else
                {
                    a_twainpixelformat.m_capabilityAutodeskew.AddValue(szValue, swordvalue);
                }
            }
        }

        /// <summary>
        /// Set the brightness...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetBrightness(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            bool blFirst = true;
            int iSwordValue;
            string szValue;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                szValue = "ICAP_BRIGHTNESS,TWON_ONEVALUE,TWTY_FIX32," + swordvalue.m_szValue;

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityBrightness = new Capability(szValue, "brightness", a_swordattribute.m_aswordvalue[iSwordValue]);
                }
                else
                {
                    a_twainpixelformat.m_capabilityBrightness.AddValue(szValue, a_swordattribute.m_aswordvalue[iSwordValue]);
                }
            }
        }

        /// <summary>
        /// Set the contrast...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetContrast(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            bool blFirst = true;
            int iSwordValue;
            string szValue;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                szValue = "ICAP_CONTRAST,TWON_ONEVALUE,TWTY_FIX32," + swordvalue.m_szValue;

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityContrast = new Capability(szValue, "contrast", swordvalue);
                }
                else
                {
                    a_twainpixelformat.m_capabilityContrast.AddValue(szValue, swordvalue);
                }
            }
        }

        /// <summary>
        /// Set the compression...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetCompression(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            bool blFirst = true;
            int iSwordValue;
            string szValue;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                switch (swordvalue.m_szValue)
                {
                    default:
                        // We're sort of hoping this will be a bad value...
                        szValue = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,-9999";
                        break;
                    case "group4":
                        szValue = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,5";
                        break;
                    case "jpeg":
                        szValue = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,6";
                        break;
                    case "none":
                        szValue = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,0";
                        break;
                }

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityCompression = new Capability(szValue, "compression", swordvalue);
                }
                else
                {
                    a_twainpixelformat.m_capabilityCompression.AddValue(szValue, swordvalue);
                }
            }
        }

        /// <summary>
        /// Set the image count (old TWAIN stuff)...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetImageCount(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            int iSwordValue;
            string szValue;
            bool blFirst = true;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                switch (a_swordattribute.m_aswordvalue[iSwordValue].m_szValue)
                {
                    default:
                        szValue = "CAP_XFERCOUNT,TWON_ONEVALUE,TWTY_INT32," + swordvalue.m_szValue;
                        break;
                    case "0":
                        szValue = "CAP_XFERCOUNT,TWON_ONEVALUE,TWTY_INT32,-1";
                        break;
                }

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityXfercount = new Capability(szValue, "imagecount", swordvalue);
                }
                else
                {
                    a_twainpixelformat.m_capabilityXfercount.AddValue(szValue, swordvalue);
                }
            }
        }

        /// <summary>
        /// Set the resolution...
        /// </summary>
        /// <param name="a_twainpixelformat">the place to store the data</param>
        /// <param name="a_swordattribute">the data</param>
        private void SetResolution(ref SwordTask a_swordtask, ref TwainPixelFormat a_twainpixelformat, ref SwordAttribute a_swordattribute)
        {
            int iSwordValue;
            string szValue;
            bool blFirst = true;

            // All the values...
            for (iSwordValue = 0; iSwordValue < a_swordattribute.m_aswordvalue.Length; iSwordValue++)
            {
                SwordValue swordvalue = a_swordattribute.m_aswordvalue[iSwordValue];

                // Vendor check, skip stuff that we don't recognize...
                if (a_swordtask.GetGuidOwner(swordvalue.m_szVendor) == SwordTask.GuidOwner.Unknown)
                {
                    TWAINWorkingGroup.Log.Info("Skipping vendor specific value...");
                    continue;
                }

                // Set the value...
                szValue = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + swordvalue.m_szValue;

                // Dispatch...
                if (blFirst)
                {
                    blFirst = false;
                    a_twainpixelformat.m_capabilityResolution = new Capability(szValue, "resolution", swordvalue);
                }
                else
                {
                    a_twainpixelformat.m_capabilityResolution.AddValue(szValue, swordvalue);
                }
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        // Contingencies...
        public TwainAction[] m_twainaction;

        #endregion
    }

    /// <summary>
    /// An action contains a command and any data associated with that command.
    /// 
    /// A task can have more than one action, and if so, each is run in turn
    /// until there are no more actions or until an action fails with a exception
    /// that requires the ending of the task.
    /// </summary>
    public sealed class TwainAction
    {
        public TwainAction(SwordAction a_swordaction)
        {
            m_szException = null;
            m_szJsonKey = null;
            m_guidVendor = Guid.Empty;
            m_szAction = null;
            m_swordaction = a_swordaction;
            m_twainstream = null;
        }

        /// <summary>
        /// Add a stream to this action.  This is used with the "configure" command...
        /// </summary>
        /// <returns></returns>
        public TwainStream AddStream(SwordStream a_swordstream)
        {
            // Add a TWAIN stream...
            if (m_twainstream == null)
            {
                m_twainstream = new TwainStream[1];
            }
            else
            {
                TwainStream[] atwainstream = new TwainStream[m_twainstream.Length + 1];
                m_twainstream.CopyTo(atwainstream, 0);
                m_twainstream = atwainstream;
            }
            m_twainstream[m_twainstream.Length - 1] = new TwainStream(a_swordstream);
            TwainStream twainstream = m_twainstream[m_twainstream.Length - 1];

            // Remember our relationship to this SWORD item...
            twainstream.m_swordstream = a_swordstream;

            // Copy the index, exception and the vendor...
            twainstream.m_szJsonKey = a_swordstream.m_szJsonKey;
            twainstream.m_szException = a_swordstream.m_szException;
            twainstream.m_guidVendor = TwainTask.ConvertStringToGuid(a_swordstream.m_szVendor);

            // All done...
            return (twainstream);
        }

        // Controls...
        public string m_szException;
        public string m_szJsonKey;
        public Guid m_guidVendor;
        public string m_szAction;

        // SWORD...
        public SwordAction m_swordaction;

        // Sources...
        public TwainStream[] m_twainstream;
    }

    /// <summary>
    /// A stream contains one or more sources.  A source can be best thought of
    /// as a physical element that records image data.  Like a feeder or a flatbed.
    /// 
    /// In many cases there will be just one source.  The most notable exception
    /// is when independent control is needed for the feederfront and feederrear
    /// sources.
    /// 
    /// Every source is potentially capability of supplying one or more images
    /// per side of a sheet of paper, and in various image formats.  This is the
    /// level where multistream output is managed.
    /// </summary>
    public sealed class TwainStream
    {
        public TwainStream(SwordStream a_swordstream)
        {
            m_szException = null;
            m_szJsonKey = null;
            m_guidVendor = Guid.Empty;
            m_swordstream = a_swordstream;
            m_twainsource = null;
        }

        /// <summary>
        /// Add a source to this stream...
        /// </summary>
        /// <returns></returns>
        public TwainSource AddSource(SwordSource a_swordsource)
        {
            // Add a TWAIN source...
            if (m_twainsource == null)
            {
                m_twainsource = new TwainSource[1];
            }
            else
            {
                TwainSource[] atwainsource = new TwainSource[m_twainsource.Length + 1];
                m_twainsource.CopyTo(atwainsource, 0);
                m_twainsource = atwainsource;
            }
            m_twainsource[m_twainsource.Length - 1] = new TwainSource(a_swordsource);

            // All done...
            return (m_twainsource[m_twainsource.Length - 1]);
        }

        // Controls...
        public string m_szException;
        public string m_szJsonKey;
        public Guid m_guidVendor;

        // SWORD...
        public SwordStream m_swordstream;

        // Sources...
        public TwainSource[] m_twainsource;
    }

    /// <summary>
    /// A source contains one or more imageformats.  An imageformat
    /// can be thought of as an instruction to a source about the
    /// amount of data that it should capture.  For instance, 24-bits
    /// of color with 8 bits for red, 8 bits for green and 8-bits for
    /// blue.  Or 8-bit grayscale, or monochromatic black-and-white
    /// with 8 pixels packed into each byte of data.
    /// 
    /// In most cases a source will ask for one format.  If it asks
    /// for more than one format that is taken as a request for the
    /// scanner to select the best format that matches the image that
    /// it captured (sometimes also called automatic color detection).
    /// </summary>
    public sealed class TwainSource
    {
        public TwainSource(SwordSource a_swordsource)
        {
            // Controls...
            m_szException = null;
            m_szJsonKey = null;
            m_guidVendor = Guid.Empty;
            m_swordsource = a_swordsource;

            // Imagesource address...
            m_capabilityAutomaticsensemedium = null;
            m_capabilityFeederenabled = null;
            m_capabilityDuplexenabled = null;

            // Imageformats...
            m_twainformat = null;

            // Copy the index, exception and the vendor...
            m_szJsonKey = a_swordsource.m_szJsonKey;
            m_szException = a_swordsource.m_szException;
            m_guidVendor = TwainTask.ConvertStringToGuid(a_swordsource.m_szVendor);
        }

        /// <summary>
        /// Set the source...
        /// </summary>
        /// <param name="a_szAutomaticSenseMedium">CAP_AUTOMATICMEDIUM setting</param>
        /// <param name="a_szDuplexEnabled">CAP_DUPLEXENABLED setting</param>
        /// <param name="a_szFeederEnabled">CAP_FEEDERENABLED setting</param>
        /// <param name="a_szSwordValue">original SWORD value</param>
        public void SetSource(string a_szAutomaticSenseMedium, string a_szDuplexEnabled, string a_szFeederEnabled, string a_szSwordValue)
        {
            // Automatic sense medium...
            if (a_szAutomaticSenseMedium == null)
            {
                m_capabilityAutomaticsensemedium = null;
            }
            else
            {
                m_capabilityAutomaticsensemedium = new Capability(a_szAutomaticSenseMedium, "source", a_szSwordValue, m_szException, m_szJsonKey, m_guidVendor);
            }

            // Duplex enabled...
            if (a_szDuplexEnabled == null)
            {
                m_capabilityDuplexenabled = null;
            }
            else
            {
                m_capabilityDuplexenabled = new Capability(a_szDuplexEnabled, "source", a_szSwordValue, m_szException, m_szJsonKey, m_guidVendor);
            }

            // Feeder enabled...
            if (a_szFeederEnabled == null)
            {
                m_capabilityFeederenabled = null;
            }
            else
            {
                m_capabilityFeederenabled = new Capability(a_szFeederEnabled, "source", a_szSwordValue, m_szException, m_szJsonKey, m_guidVendor);
            }
        }

        /// <summary>
        /// Add a format to this source...
        /// </summary>
        /// <param name="a_swordpixelformat"></param>
        /// <returns></returns>
        public TwainPixelFormat AddPixelFormat(SwordPixelFormat a_swordpixelformat)
        {
            // Add a TWAIN format...
            if (m_twainformat == null)
            {
                m_twainformat = new TwainPixelFormat[1];
            }
            else
            {
                TwainPixelFormat[] atwainformat = new TwainPixelFormat[m_twainformat.Length + 1];
                m_twainformat.CopyTo(atwainformat, 0);
                m_twainformat = atwainformat;
            }
            m_twainformat[m_twainformat.Length - 1] = new TwainPixelFormat(a_swordpixelformat);

            // Copy the index, exception and the vendor...
            m_twainformat[m_twainformat.Length - 1].m_szJsonKey = a_swordpixelformat.m_szJsonKey;
            m_twainformat[m_twainformat.Length - 1].m_szException = a_swordpixelformat.m_szException;
            m_twainformat[m_twainformat.Length - 1].m_guidVendor = TwainTask.ConvertStringToGuid(a_swordpixelformat.m_szVendor);

            // All done...
            return (m_twainformat[m_twainformat.Length - 1]);
        }

        /// <summary>
        /// Make a note of the JSON index from SWORD...
        /// </summary>
        /// <param name="a_szJsonIndex">the dotted notation for this item</param>
        public void SetJsonIndex(string a_szJsonIndex)
        {
            m_szJsonKey = a_szJsonIndex;
        }

        /// <summary>
        /// Set the source...
        /// </summary>
        /// <param name="a_twaincstoolkit">the toolkit we're using</param>
        /// <param name="a_guidVendor">the vendor, if any</param>
        /// <param name="a_blAutomaticSenseMedium">auto detect source</param>
        /// <param name="a_blFeederEnabled">using the adf</param>
        /// <param name="a_szSource">the source we picked</param>
        /// <param name="a_swordtask">the task object</param>
        /// <returns>the status as a string</returns>
        public string SetSource
        (
            TWAINCSToolkit a_twaincstoolkit,
            Guid a_guidVendor,
            bool a_blAutomaticSenseMedium,
            bool a_blFeederEnabled,
            out string a_szSource,
            ref SwordTask a_swordtask
        )
        {
            string szStatus;
            string szSwordName;
            string szSwordValue;
            string szTwainValue;
            bool blDoFeederEnabled;

            // Init it...
            a_szSource = "";

            // If the source is custom, and custom isn't us, then skip it...
            if (    (m_guidVendor != Guid.Empty)
                &&  (m_guidVendor != a_guidVendor))
            {
                return ("skip");
            }

            // Assume "any" until something overrides it...
            a_szSource = "any";

            // Set automatic sense medium...
            blDoFeederEnabled = true;
            if (    a_blAutomaticSenseMedium
                &&  (m_capabilityAutomaticsensemedium != null))
            {
                // Set the value...
                szStatus = m_capabilityAutomaticsensemedium.SetScanner(a_twaincstoolkit, a_guidVendor, out szSwordName, out szSwordValue, out szTwainValue, ref a_swordtask);
                if (szStatus != "success")
                {
                    return (szStatus);
                }

                // Check the result...
                szStatus = "";
                string szCapability = "CAP_AUTOMATICSENSEMEDIUM";
                TWAINCSToolkit.STS sts = a_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts == TWAINCSToolkit.STS.SUCCESS)
                {
                    string[] asz = CSV.Parse(szCapability);
                    if ((asz != null) && (asz.Length == 4) && (asz[3] == "1"))
                    {
                        blDoFeederEnabled = false;
                    }
                }
            }

            // Pick a physical source (feeder or flatbed)...
            if (blDoFeederEnabled && (m_capabilityFeederenabled != null))
            {
                // Set the feeder on or off...
                szStatus = m_capabilityFeederenabled.SetScanner(a_twaincstoolkit, a_guidVendor, out szSwordName, out szSwordValue, out szTwainValue, ref a_swordtask);
                if (szStatus != "success")
                {
                    return (szStatus);
                }

                // Okay, what the heck did we pick?
                if (szTwainValue.EndsWith(",1"))
                {
                    a_szSource = "feeder";
                }
                else
                {
                    a_szSource = "flatbed";
                }
            }

            // Simplex or Duplex...
            if (m_capabilityDuplexenabled != null)
            {
                szStatus = m_capabilityDuplexenabled.SetScanner(a_twaincstoolkit, a_guidVendor, out szSwordName, out szSwordValue, out szTwainValue, ref a_swordtask);
                if (szStatus != "success")
                {
                    return (szStatus);
                }

                // TBD
                // Okay, what the heck did we pick?
                // This is a bit messed up, because we can get a success from SetScanner, even
                // though the command failed (due to exception processing).  One way around this
                // would be to do a getcurrent operation on failure, but that assumes the
                // capability is supported, and so we would need a fallback.  So for the moment
                // we'll key off the fact that szTwainValue is null when this happens...
                if ((szTwainValue == null) || szTwainValue.EndsWith(",0"))
                {
                    a_szSource = "feederFront";
                }
            }

            // We're good...
            return ("success");
        }

        // Controls...
        private string m_szException;
        private string m_szJsonKey;
        private Guid m_guidVendor;
        private SwordSource m_swordsource;

        // Imagesource address...
        //
        // TBD: We'll need an option for DAT_FILESYSTEM at this level if
        // we ever want to support rear-only scanning...
        private Capability m_capabilityAutomaticsensemedium;
        private Capability m_capabilityFeederenabled;
        private Capability m_capabilityDuplexenabled;

        // Imageformats...
        public TwainPixelFormat[] m_twainformat;
    }

    /// <summary>
    /// Each imageformat contains all the settings allowed for that format.
    /// There is no distinction between machine settings and imageformat
    /// settings.  This makes the system more wordy, but it also allows it
    /// to seamlessly scale and to handle situations where capabilities
    /// may vary is their depths among products.
    /// 
    /// A simple example is printing.  One scanner may only support a single
    /// print string for the scan session, while another may support
    /// different text for the front and the rear.
    /// 
    /// It's not expected that applications will rely on this to handle all
    /// of the possible permutations one can run into with capabilities.
    /// More likely an application will target the extended features of a
    /// given scanner, and this architecture makes it easy to set it up
    /// without any ambiguity about where it goes.
    /// 
    /// However, in the event that a scanner with support for a single text
    /// string was confronted with several different strings, it is only
    /// obligated to use the first one it finds.  All others can be ignored.
    /// </summary>
    public sealed class TwainPixelFormat
    {
        /// <summary>
        /// Our constructor...
        /// </summary>
        public TwainPixelFormat(SwordPixelFormat a_swordpixelformat)
        {
            // Controls...
            m_szException = null;
            m_szJsonKey = null;
            m_guidVendor = Guid.Empty;
            m_swordpixelformat = a_swordpixelformat;

            // Imageformat stuff...
            m_capabilityAutocolorenabled = null;
            m_capabilityAutomaticcolornoncolorpixeltype = null;
            m_capabilityPixeltype = null;

            // Capabilities...
            m_capabilityAutocrop = null;
            m_capabilityAutodeskew = null;
            m_capabilituBitdepthreduction = null;
            m_capabilityBrightness = null;
            m_capabilityCompression = null;
            m_capabilityContrast = null;
            m_capabilityCrop = null;
            m_capabilityDoublefeed = null;
            m_capabilityResolution = null;
            m_capabilityXfercount = null;
        }

        // Controls...
        public string m_szException;
        public string m_szJsonKey;
        public Guid m_guidVendor;
        public SwordPixelFormat m_swordpixelformat;

        // Autoimageformat, we turn it on and we select the
        // kind of black-and-white image we want: gray8 or bw1...
        public Capability m_capabilityAutocolorenabled;
        public Capability m_capabilityAutomaticcolornoncolorpixeltype;

        // Imageformat...
        public Capability m_capabilityPixeltype;

        // Capabilities...
        public Capability m_capabilityAutocrop;
        public Capability m_capabilityAutodeskew;
        public Capability m_capabilituBitdepthreduction;
        public Capability m_capabilityBrightness;
        public Capability m_capabilityCompression;
        public Capability m_capabilityContrast;
        public Capability m_capabilityCrop;
        public Capability m_capabilityDoublefeed;
        public Capability m_capabilityResolution;
        public Capability m_capabilityXfercount;
    }

    /// <summary>
    /// A capability contains one or more values, which will be tried in order until
    /// the scanner accepts one, or we run out.
    /// </summary>
    public sealed class Capability
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init the object...
        /// </summary>
        /// <param name="a_szCapability">the TWAIN capability we'll be using</param>
        /// <param name="a_szSwordName">the SWORD name</param>
        /// <param name="a_szException">the SWORD value</param>
        /// <param name="a_szException">the SWORD exception</param>
        /// <param name="a_szJsonIndex">the location of the data in the original JSON string</param>
        /// <param name="a_guidVendor">the vendor for this item</param>
        public Capability(string a_szCapability, string a_szSwordName, string a_szSwordValue, string a_szException, string a_szJsonIndex, Guid a_guidVendor)
        {
            // Init value...
            m_acapabilityvalue = null;

            // Seed stuff...
            if ((a_szCapability != null) && (a_szCapability.Length > 0))
            {
                m_acapabilityvalue = new CapabilityValue[1];
                m_acapabilityvalue[0] = new CapabilityValue(a_szCapability, a_szSwordName, a_szSwordValue, a_szException, a_szJsonIndex, a_guidVendor);
            }
        }

        /// <summary>
        /// Init the object...
        /// </summary>
        /// <param name="a_szCapability">the TWAIN capability we'll be using</param>
        /// <param name="a_twainpixelformat">value to use</param>
        public Capability(string a_szCapability, SwordPixelFormat a_swordpixelformat, TwainPixelFormat a_twainpixelformat)
        {
            // Init value...
            m_acapabilityvalue = null;

            // Seed stuff...
            if ((a_szCapability != null) && (a_szCapability.Length > 0))
            {
                m_acapabilityvalue = new CapabilityValue[1];
                m_acapabilityvalue[0] = new CapabilityValue
                (
                    a_szCapability,
                    "pixelFormat",
                    a_swordpixelformat.m_szPixelFormat,
                    a_twainpixelformat.m_szException,
                    a_twainpixelformat.m_szJsonKey,
                    a_twainpixelformat.m_guidVendor
                );
            }
        }

        /// <summary>
        /// Init the object...
        /// </summary>
        /// <param name="a_szCapability">the TWAIN capability we'll be using</param>
        /// <param name="a_swordvalue">value to use</param>
        public Capability(string a_szCapability, string a_szSwordName, SwordValue a_swordvalue)
        {
            // Init value...
            m_acapabilityvalue = null;

            // Seed stuff...
            if ((a_szCapability != null) && (a_szCapability.Length > 0))
            {
                m_acapabilityvalue = new CapabilityValue[1];
                m_acapabilityvalue[0] = new CapabilityValue
                (
                    a_szCapability,
                    a_szSwordName,
                    a_swordvalue.m_szValue,
                    a_swordvalue.m_szException,
                    a_swordvalue.m_szJsonKey,
                    TwainTask.ConvertStringToGuid(a_swordvalue.m_szVendor)
                );
            }
        }
        
        /// <summary>
        /// Add another value to the capability.  We'll be trying them in order
        /// until we find one that works, or until we run out.
        /// </summary>
        /// <param name="a_szCapability">the TWAIN capability we'll be using</param>
        /// <param name="a_swordvalue">value to use</param>
        public void AddValue(string a_szCapability, SwordValue a_swordvalue)
        {
            // Seed stuff...
            if ((a_szCapability != null) && (a_szCapability.Length > 0))
            {
                CapabilityValue[] acapabilityvalue = new CapabilityValue[m_acapabilityvalue.Length + 1];
                m_acapabilityvalue.CopyTo(acapabilityvalue, 0);
                acapabilityvalue[m_acapabilityvalue.Length] = new CapabilityValue
                (
                    a_szCapability,
                    acapabilityvalue[0].GetSwordName(),
                    a_swordvalue.m_szValue,
                    a_swordvalue.m_szException,
                    a_swordvalue.m_szJsonKey,
                    TwainTask.ConvertStringToGuid(a_swordvalue.m_szVendor)
                );
                m_acapabilityvalue = acapabilityvalue;
            }
        }

        /// <summary>
        /// Set the scanner...
        /// </summary>
        /// <param name="a_twaincstoolkit">toolkit object</param>
        /// <param name="a_guid">vendor GUID</param>
        /// <param name="a_szSwordName">the SWORD name we picked</param>
        /// <param name="a_szSwordValue">the SWORD value we picked</param>
        /// <param name="a_szTwainValue">the TWAIN value we picked</param>
        /// <param name="a_swordtask">the task object</param>
        /// <returns></returns>
        public string SetScanner
        (
            TWAINCSToolkit a_twaincstoolkit,
            Guid a_guid,
            out string a_szSwordName,
            out string a_szSwordValue,
            out string a_szTwainValue,
            ref SwordTask a_swordtask
        )
        {
            int iTryValue;
            string szStatus;
            string szTwainValue;
            TWAINCSToolkit.STS sts;

            // Init stuff...
            szTwainValue = "";
            a_szSwordName = null;
            a_szSwordValue = null;
            a_szTwainValue = null;

            // Keep trying till we set something...
            sts = TWAINCSToolkit.STS.SUCCESS;
            for (iTryValue = 0; iTryValue < m_acapabilityvalue.Length; iTryValue++)
            {
                // Skip stuff that isn't ours...
                if (    (m_acapabilityvalue[iTryValue].GetGuidVendor() != Guid.Empty)
                    &&  (m_acapabilityvalue[iTryValue].GetGuidVendor() != a_guid))
                {
                    continue;
                }

                // Try to set the value...
                szStatus = "";
                szTwainValue = m_acapabilityvalue[iTryValue].GetCapability();
                sts = a_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szTwainValue, ref szStatus);
                if (sts == TWAINCSToolkit.STS.SUCCESS)
                {
                    a_szSwordName = m_acapabilityvalue[iTryValue].GetSwordName();
                    a_szSwordValue = m_acapabilityvalue[iTryValue].GetSwordValue();
                    a_szTwainValue = szTwainValue;
                    break;
                }
            }
            
            // TBD
            // This section is messed up, try to get the ICAP_YRESOLUTION
            // handler out of here...
            if (    (szTwainValue != "")
                &&  ((sts == TWAINCSToolkit.STS.SUCCESS) || (sts == TWAINCSToolkit.STS.CHECKSTATUS)))
            {
                string[] asz = CSV.Parse(szTwainValue);
                switch (asz[0])
                {
                    // All done...
                    default:
                        return ("success");

                    // Handle ICAP_XRESOLUTION/ICAP_YRESOLUTION...
                    case "ICAP_XRESOLUTION":
                        szStatus = "";
                        szTwainValue = m_acapabilityvalue[iTryValue].GetCapability().Replace("ICAP_XRESOLUTION", "ICAP_YRESOLUTION");
                        sts = a_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szTwainValue, ref szStatus);
                        break;
                }

                // We're good...
                if (    (sts == TWAINCSToolkit.STS.SUCCESS)
                    ||  (sts == TWAINCSToolkit.STS.CHECKSTATUS))
                {
                    return ("success");
                }
            }

            // We ran into a problem, make sure that we're looking at valid value
            // in array (usually the last item)...
            if (iTryValue >= m_acapabilityvalue.Length)
            {
                iTryValue = m_acapabilityvalue.Length - 1;
            }

            // Handle the exception...
            switch (m_acapabilityvalue[iTryValue].GetException())
            {
                // Do nothing, stick with the current value, this includes if we
                // don't recognize the exception, because TWAIN Direct is supposed
                // to emphasize success...
                default:
                case "ignore":
                    return ("success");

                // Pass the item up...
                case "fail":
                    if (string.IsNullOrEmpty(szTwainValue))
                    {
                        a_swordtask.SetTaskError(m_acapabilityvalue[iTryValue].GetException(), m_acapabilityvalue[iTryValue].GetJsonKey(), null, -1);
                    }
                    else
                    {
                        a_swordtask.SetTaskError(m_acapabilityvalue[iTryValue].GetException(), m_acapabilityvalue[iTryValue].GetJsonKey(), szTwainValue, -1);
                    }
                    return (m_acapabilityvalue[iTryValue].GetException());

                // Pass the item up...
                case "nextStream":
                    return (m_acapabilityvalue[iTryValue].GetException());
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        // The array of values to try...
        private CapabilityValue[] m_acapabilityvalue;

        #endregion
    }

    /// <summary>
    /// A capability value contains all the stuff it needs to try to set a value...
    /// </summary>
    public sealed class CapabilityValue
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init the object...
        /// </summary>
        /// <param name="a_szCapability">The TWAIN setting in CSV format</param>
        /// <param name="a_szSwordName">the SWORD name</param>
        /// <param name="a_szSwordValue">the SWORD value</param>
        /// <param name="a_szException">the SWORD exception</param>
        /// <param name="a_szJsonIndex">the location of the data in the original JSON string</param>
        /// <param name="a_guidVendor">the vendor for this item</param>
        public CapabilityValue(string a_szCapability, string a_szSwordName, string a_szSwordValue, string a_szException, string a_szJsonIndex, Guid a_guidVendor)
        {
            // Controls...
            m_szCapability = a_szCapability;
            m_szSwordName = a_szSwordName;
            m_szSwordValue = a_szSwordValue;
            m_szException = a_szException;
            m_szJsonKey = a_szJsonIndex;
            m_guidVendor = a_guidVendor;
        }

        /// <summary>
        /// Return the TWAIN setting in CSV format...
        /// </summary>
        /// <returns>YWAIN capability</returns>
        public string GetCapability()
        {
            return (m_szCapability);
        }

        /// <summary>
        /// Return the exception...
        /// </summary>
        /// <returns>exception</returns>
        public string GetException()
        {
            return (m_szException);
        }

        /// <summary>
        /// Return vendor GUID...
        /// </summary>
        /// <returns>key in dotted notation</returns>
        public Guid GetGuidVendor()
        {
            return (m_guidVendor);
        }

        /// <summary>
        /// Return the JSON key to this item...
        /// </summary>
        /// <returns>key in dotted notation</returns>
        public string GetJsonKey()
        {
            return (m_szJsonKey);
        }

        /// <summary>
        /// Return the SWORD name to this item...
        /// </summary>
        /// <returns>SWORD name</returns>
        public string GetSwordName()
        {
            return (m_szSwordName);
        }

        /// <summary>
        /// Return the SWORD value to this item...
        /// </summary>
        /// <returns>SWORD value</returns>
        public string GetSwordValue()
        {
            return (m_szSwordValue);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// The TWAIN MSG_SET command in CSV format...
        /// </summary>
        private string m_szCapability;

        /// <summary>
        /// The name of this sword item...
        /// </summary>
        private string m_szSwordName;

        /// <summary>
        /// The value to report back when building the task reply...
        /// </summary>
        private string m_szSwordValue;

        /// <summary>
        /// The TWAIN Direct exception for this value...
        /// </summary>
        private string m_szException;

        // The dotted key notation to locate this item in the original task...
        private string m_szJsonKey;

        /// <summary>
        /// The vendor owning this value...
        /// </summary>
        private Guid m_guidVendor;

        #endregion
    }

    #endregion
}
