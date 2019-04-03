///////////////////////////////////////////////////////////////////////////////////////
//
//  TwainDirect.OnTwain.SwordTask
//
//  This class follows the full lifecycle of a TWAIN Direct task, carrying its
//  inputs and support data, and returning the result.  The theory is we have
//  a one-stop shop for everything we want to do in a task, and functions don't
//  need any other context than what this class brings with it.
//
//  Compare this class to ApiCmd for a similiar concept in TwainDirect.Scanner.
//
//  The name SWORD (Scanning WithOut Requiring Drivers) was superceded by the
//  name TWAIN Direct.  However, we're doing TWAIN stuff in this assembly, and
//  the names are too close for comfort, which is why SWORD is still in use...
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    29-Jun-2014     Splitting up files...
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2018 Kodak Alaris Inc.
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
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using TwainDirect.Support;
using TWAINWorkingGroup;
using TWAINWorkingGroupToolkit;

namespace TwainDirect.OnTwain
{
    /// <summary>
    /// Process a TWAIN Direct task...
    /// </summary>
    #region ProcessSwordTask

    /// <summary>
    /// This SWORD object wraps all of the JSON content...
    /// </summary>
    internal sealed class ProcessSwordTask : IDisposable
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init stuff...
        /// </summary>
        /// <param name="a_szImagesFolder">place to put images</param>
        /// <param name="a_twaincstoolkit">if not null, the toolkit the caller wants us to use</param>
        /// <param name="a_deviceregister">info about the TWAIN driver</param>
        public ProcessSwordTask(string a_szImagesFolder, TWAINCSToolkit a_twaincstoolkit, DeviceRegister a_deviceregister)
        {
            // Init stuff...
            m_szImagesFolder = a_szImagesFolder;
            m_twaincstoolkit = a_twaincstoolkit;
            m_twaincstoolkitCaller = a_twaincstoolkit;
            m_deviceregister = (a_deviceregister != null) ? a_deviceregister : new DeviceRegister();
            m_blDuplexEnabled = false;

            // Our response object for errors and success...
            m_swordtaskresponse = new SwordTaskResponse(this);

            /// The TWAIN Direct vendor id...
            m_szVendorTwainDirect = "211a1e90-11e1-11e5-9493-1697f925ec7b";

            /// The scanner's vendor id, until we get the real value...
            m_szVendor = "00000000-0000-0000-0000-000000000000";
        }

        /// <summary>
        /// Cleanup...
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Close sword, free all resources...
        /// </summary>
        public void Close()
        {
            Dispose(true);
        }

        /// <summary>
        /// Turn a JSON string into something we can deal with.  Specifically,
        /// the structure described in the SWORD documentation.  This structure
        /// takes on the topology of the request.  This means that the only
        /// elements we're going to find in it are the ones requested by the
        /// caller.
        /// 
        /// We have options at that point.  If the device is already set to
        /// the desired preset (such as factory default), then we only have
        /// to apply the task to it.  In this case the developer may opt to
        /// find the preset and send that down, then follow it with the rest
        /// of the contents within the task.
        /// 
        /// On the other paw it might be easier for some developers to merge
        /// the task with the relevant baseline settings and fire the whole
        /// thing over to the device.  Bearing in mind that they may need to
        /// some merging on the other side when they try to construct the
        /// metadata that goes with the image.
        /// </summary>
        /// <param name="a_szTask">task to process</param>
        /// <param name="a_szVendor">this scanner's vendor id</param>
        /// <returns>true on success</returns>
        public bool Deserialize(string a_szTask, string a_szVendor)
        {
            int iAction;
            int iStream;
            int iSource;
            int iPixelFormat;
            int iAttribute;
            int iValue;
            int iEncryptionProfile;
            bool blSuccess;
            string szSwordAction;
            string szSwordStream;
            string szSwordSource;
            string szSwordPixelformat;
            string szSwordAttribute;
            string szSwordValue;
            string szRequestedAction;
            string szSwordEncryptionProfile;
            long lResponseCharacterOffset;
            JsonLookup.EPROPERTYTYPE epropertytype;
            SwordAction swordaction;
            SwordStream swordstream;
            SwordSource swordsource;
            SwordPixelFormat swordpixelformat;
            SwordAttribute swordattribute;
            SwordValue swordvalue;
            SwordEncryptionProfile swordencryptionprofile;
            string szFunction = "Deserialize";

            // Parse the JSON that we get back...
            m_jsonlookupTask = new JsonLookup();
            blSuccess = m_jsonlookupTask.Load(a_szTask, out lResponseCharacterOffset);
            if (!blSuccess)
            {
                TWAINWorkingGroup.Log.Error(szFunction + ": Load failed...");
                m_swordtaskresponse.SetError("fail", null, "invalidJson", lResponseCharacterOffset);
                return (false);
            }

            // Instantiate the sword object...
            m_szVendor = a_szVendor;
            m_swordtask = new SwordTask(this);

            // Grab the locale, if we have one...
            m_swordtask.SetLocale(m_jsonlookupTask.Get("locale", false));

            // Check the type of actions (make sure we find actions), if this
            // fails we need to make a temporary action to carry the error...
            epropertytype = m_jsonlookupTask.GetType("actions");
            if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
            {
                TWAINWorkingGroup.Log.Error("topology violation: actions isn't an array");
                m_swordtaskresponse.SetError("fail", "actions", "invalidTask", -1, true);
                m_swordtask.BuildTaskReply();
                return (false);
            }

            // Walk the data.  As we walk it, we're going to build our data
            // structure, and do the following:
            //
            // - data items defined in outer levels cascade into inner
            //   levels, like exception
            //
            // - unrecognized content is discarded (types will fall into this
            //   category)
            //
            // - discard unrecognized vendor content...
            //
            // When done we'll have a structure that will already show some
            // of the culling process.  That process will continue as we go
            // on to try to set up the scanner.
            for (iAction = 0; true; iAction++)
            {
                // Break when we run out of actions...
                szSwordAction = "actions[" + iAction + "]";
                if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordAction,false)))
                {
                    break;
                }

                // Add to the action array, skip vendor stuff we don't recognize...
                string szActionException = m_jsonlookupTask.Get(szSwordAction + ".exception",false);
                string szActionVendor = m_jsonlookupTask.Get(szSwordAction + ".vendor",false);
                swordaction = m_swordtask.AppendAction(szSwordAction, m_jsonlookupTask.Get(szSwordAction + ".action", false), szActionException, szActionVendor);
                if (swordaction == null)
                {
                    continue;
                }

                // Check the topology...
                if (    !CheckTopology(swordaction, "actions", "")
                    ||  !CheckTopology(swordaction, "action", szSwordAction))
                {
                    m_swordtask.BuildTaskReply();
                    return (false);
                }

                // Set the status...
                swordaction.SetSwordStatus(SwordStatus.Ready);

                // Which action are we deserializing?
                szRequestedAction = m_jsonlookupTask.Get(szSwordAction + ".action", false);

                // Handle the encryptionProfiles action...
                #region Handle the encryptionProfiles action...
                if (szRequestedAction == "encryptionProfiles")
                {
                    // Check the type of encryptionProfiles (make sure we find encryptionProfiles)...
                    epropertytype = m_jsonlookupTask.GetType(szSwordAction + ".encryptionProfiles");
                    if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                    {
                        TWAINWorkingGroup.Log.Error("topology violation: encryptionProfiles isn't an array");
                        swordaction.SetError("fail", szSwordAction + ".encryptionProfiles", "invalidTask", -1);
                        m_swordtask.BuildTaskReply();
                        return (false);
                    }

                    ////////////////////////////////////////////////////////////////////////
                    // Work on encryption profiles...
                    for (iEncryptionProfile = 0; true; iEncryptionProfile++)
                    {
                        // Break when we run out of encryptionProfiles...
                        szSwordEncryptionProfile = szSwordAction + ".encryptionProfiles[" + iEncryptionProfile + "]";
                        if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordEncryptionProfile, false)))
                        {
                            break;
                        }

                        // Add to the encryption profile array, skip vendor stuff we don't recognize...
                        string szEncryptionProfileException = m_jsonlookupTask.Get(szSwordEncryptionProfile + ".exception", false);
                        string szEncryptionProfileVendor = m_jsonlookupTask.Get(szSwordEncryptionProfile + ".vendor", false);
                        if (string.IsNullOrEmpty(szEncryptionProfileException)) szEncryptionProfileException = swordaction.GetException();
                        if (string.IsNullOrEmpty(szEncryptionProfileVendor)) szEncryptionProfileVendor = swordaction.GetVendor();
                        swordencryptionprofile = swordaction.AppendEncryptionProfile
                        (
                            szSwordEncryptionProfile,
                            m_jsonlookupTask.Get(szSwordEncryptionProfile + ".name", false),
                            szEncryptionProfileException,
                            szEncryptionProfileVendor,
                            m_jsonlookupTask.Get(szSwordEncryptionProfile + ".profile", false)
                        );
                        if (swordencryptionprofile == null)
                        {
                            continue;
                        }

                        // Check the topology...
                        if (!CheckTopology(swordaction, "encryptionProfiles", szSwordAction))
                        {
                            m_swordtask.BuildTaskReply();
                            return (false);
                        }

                        // Set the status...
                        swordencryptionprofile.SetSwordStatus(SwordStatus.Ready);
                    }
                }
                #endregion

                // Handle the encryptionReport action...
                #region Handle the encryptionReport action...
                else if (szRequestedAction == "encryptionReport")
                {
                    // There's no data in this command, so we're done...
                }
                #endregion

                // Handle the configure action (this is the default if we have no action)...
                #region Handle the configure action (this is the default if we have no action)...
                else
                {
                    // Check the type of streams (make sure we find streams)...
                    epropertytype = m_jsonlookupTask.GetType(szSwordAction + ".streams");
                    if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                    {
                        TWAINWorkingGroup.Log.Error("topology violation: streams isn't an array");
                        swordaction.SetError("fail", szSwordAction + ".streams", "invalidTask", -1);
                        m_swordtask.BuildTaskReply();
                        return (false);
                    }

                    ////////////////////////////////////////////////////////////////////////
                    // Work on streams...
                    for (iStream = 0; true; iStream++)
                    {
                        // Break when we run out of streams...
                        szSwordStream = szSwordAction + ".streams[" + iStream + "]";
                        if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordStream, false)))
                        {
                            break;
                        }

                        // Add to the stream array, skip vendor stuff we don't recognize...
                        string szStreamException = m_jsonlookupTask.Get(szSwordStream + ".exception", false);
                        string szStreamVendor = m_jsonlookupTask.Get(szSwordStream + ".vendor", false);
                        if (string.IsNullOrEmpty(szStreamException)) szStreamException = swordaction.GetException();
                        if (string.IsNullOrEmpty(szStreamVendor)) szStreamVendor = swordaction.GetVendor();
                        swordstream = swordaction.AppendStream(szSwordStream, m_jsonlookupTask.Get(szSwordStream + ".name", false), szStreamException, szStreamVendor);
                        if (swordstream == null)
                        {
                            continue;
                        }

                        // Check the topology...
                        if (!CheckTopology(swordaction, "streams", szSwordAction)
                            || !CheckTopology(swordaction, "stream", szSwordStream))
                        {
                            m_swordtask.BuildTaskReply();
                            return (false);
                        }

                        // Set the status...
                        swordstream.SetSwordStatus(SwordStatus.Ready);

                        // Check the type of sources (make sure we find sources)...
                        epropertytype = m_jsonlookupTask.GetType(szSwordStream + ".sources");
                        if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                        {
                            TWAINWorkingGroup.Log.Error("topology violation: sources isn't an array");
                            swordstream.SetError("fail", szSwordStream + ".sources", "invalidTask", -1);
                            m_swordtask.BuildTaskReply();
                            return (false);
                        }

                        ////////////////////////////////////////////////////////////////////
                        // Work on sources...
                        for (iSource = 0; true; iSource++)
                        {
                            // Break when we run out of sources...
                            szSwordSource = szSwordStream + ".sources[" + iSource + "]";
                            if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordSource, false)))
                            {
                                break;
                            }

                            // Add to the source array, skip vendor stuff we don't recognize...
                            string szSourceException = m_jsonlookupTask.Get(szSwordSource + ".exception", false);
                            string szSourceVendor = m_jsonlookupTask.Get(szSwordSource + ".vendor", false);
                            if (string.IsNullOrEmpty(szSourceException)) szSourceException = swordstream.GetException();
                            if (string.IsNullOrEmpty(szSourceVendor)) szSourceVendor = swordstream.GetVendor();
                            swordsource = swordstream.AppendSource(szSwordSource, m_jsonlookupTask.Get(szSwordSource + ".name", false), m_jsonlookupTask.Get(szSwordSource + ".source", false), szSourceException, szSourceVendor);
                            if (swordsource == null)
                            {
                                continue;
                            }

                            // Check the topology...
                            if (!CheckTopology(swordaction, "sources", szSwordStream)
                                || !CheckTopology(swordaction, "source", szSwordSource))
                            {
                                m_swordtask.BuildTaskReply();
                                return (false);
                            }

                            // Set the status...
                            swordsource.SetSwordStatus(SwordStatus.Ready);

                            // Check the type of pixelFormats (make sure we find pixelFormats)...
                            epropertytype = m_jsonlookupTask.GetType(szSwordSource + ".pixelFormats");
                            if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                            {
                                TWAINWorkingGroup.Log.Error("topology violation: pixelFormats isn't an array");
                                swordsource.SetError("fail", szSwordSource + ".pixelFormats", "invalidTask", -1);
                                m_swordtask.BuildTaskReply();
                                return (false);
                            }

                            ////////////////////////////////////////////////////////////////
                            // Work on pixel formats...
                            for (iPixelFormat = 0; true; iPixelFormat++)
                            {
                                // Break when we run out of pixelformats...
                                szSwordPixelformat = szSwordSource + ".pixelFormats[" + iPixelFormat + "]";
                                if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordPixelformat, false)))
                                {
                                    break;
                                }

                                // Add to the pixelformat array, skip vendor stuff we don't recognize...
                                string szPixelformatException = m_jsonlookupTask.Get(szSwordPixelformat + ".exception", false);
                                string szPixelformatVendor = m_jsonlookupTask.Get(szSwordPixelformat + ".vendor", false);
                                if (string.IsNullOrEmpty(szPixelformatException)) szPixelformatException = swordsource.GetException();
                                if (string.IsNullOrEmpty(szPixelformatVendor)) szPixelformatVendor = swordsource.GetVendor();
                                swordpixelformat = swordsource.AppendPixelFormat(szSwordPixelformat, m_jsonlookupTask.Get(szSwordPixelformat + ".name", false), m_jsonlookupTask.Get(szSwordPixelformat + ".pixelFormat", false), szPixelformatException, szPixelformatVendor);
                                if (swordpixelformat == null)
                                {
                                    continue;
                                }

                                // Check the topology...
                                if (!CheckTopology(swordaction, "pixelFormats", szSwordSource)
                                    || !CheckTopology(swordaction, "pixelFormat", szSwordPixelformat))
                                {
                                    m_swordtask.BuildTaskReply();
                                    return (false);
                                }

                                // Set the status...
                                swordpixelformat.SetSwordStatus(SwordStatus.Ready);

                                // Check the type of attributes (make sure we find attributes)...
                                epropertytype = m_jsonlookupTask.GetType(szSwordPixelformat + ".attributes");
                                if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                                {
                                    TWAINWorkingGroup.Log.Error("topology violation: attributes isn't an array");
                                    swordpixelformat.SetError("fail", szSwordPixelformat + ".attributes", "invalidTask", -1);
                                    m_swordtask.BuildTaskReply();
                                    return (false);
                                }

                                ////////////////////////////////////////////////////////////
                                // Work on attributes...
                                for (iAttribute = 0; true; iAttribute++)
                                {
                                    // Break when we run out of attributes...
                                    szSwordAttribute = szSwordPixelformat + ".attributes[" + iAttribute + "]";
                                    if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordAttribute, false)))
                                    {
                                        break;
                                    }

                                    // Add to the attribute array, skip vendor stuff we don't recognize...
                                    string szAttributeException = m_jsonlookupTask.Get(szSwordAttribute + ".exception", false);
                                    string szAttributeVendor = m_jsonlookupTask.Get(szSwordAttribute + ".vendor", false);
                                    if (string.IsNullOrEmpty(szAttributeException)) szAttributeException = swordpixelformat.GetException();
                                    if (string.IsNullOrEmpty(szAttributeVendor)) szAttributeVendor = swordpixelformat.GetVendor();
                                    swordattribute = swordpixelformat.AddAttribute(szSwordAttribute, m_jsonlookupTask.Get(szSwordAttribute + ".attribute", false), szAttributeException, szAttributeVendor);
                                    if (swordattribute == null)
                                    {
                                        continue;
                                    }

                                    // Check the topology...
                                    if (!CheckTopology(swordaction, "attributes", szSwordPixelformat)
                                        || !CheckTopology(swordaction, "attribute", szSwordAttribute))
                                    {
                                        m_swordtask.BuildTaskReply();
                                        return (false);
                                    }

                                    // Set the status...
                                    swordattribute.SetSwordStatus(SwordStatus.Ready);

                                    // Check the type of values (make sure we find values)...
                                    epropertytype = m_jsonlookupTask.GetType(szSwordAttribute + ".values");
                                    if ((epropertytype != JsonLookup.EPROPERTYTYPE.ARRAY) && (epropertytype != JsonLookup.EPROPERTYTYPE.UNDEFINED))
                                    {
                                        TWAINWorkingGroup.Log.Error("topology violation: values isn't an array");
                                        swordattribute.SetError("fail", szSwordAttribute + ".values", "invalidTask", -1);
                                        m_swordtask.BuildTaskReply();
                                        return (false);
                                    }

                                    ////////////////////////////////////////////////////////
                                    // Work on values...
                                    for (iValue = 0; true; iValue++)
                                    {
                                        // Break when we run out of values...
                                        szSwordValue = szSwordAttribute + ".values[" + iValue + "]";
                                        if (string.IsNullOrEmpty(m_jsonlookupTask.Get(szSwordValue, false)))
                                        {
                                            break;
                                        }

                                        // Add to the value array, skip vendor stuff we don't recognize...
                                        string szValueException = m_jsonlookupTask.Get(szSwordValue + ".exception", false);
                                        string szValueVendor = m_jsonlookupTask.Get(szSwordValue + ".vendor", false);
                                        if (string.IsNullOrEmpty(szValueException)) szValueException = swordattribute.GetException();
                                        if (string.IsNullOrEmpty(szValueVendor)) szValueVendor = swordattribute.GetVendor();
                                        swordvalue = swordattribute.AppendValue(szSwordValue, m_jsonlookupTask.Get(szSwordValue + ".value"), szValueException, szValueVendor);
                                        if (swordvalue == null)
                                        {
                                            continue;
                                        }

                                        // Check the topology...
                                        if (!CheckTopology(swordaction, "values", szSwordAttribute)
                                            || !CheckTopology(swordaction, "value", szSwordValue))
                                        {
                                            m_swordtask.BuildTaskReply();
                                            return (false);
                                        }

                                        // Set the status...
                                        swordvalue.SetSwordStatus(SwordStatus.Ready);
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion
            }

            // Fix the exceptions.  This just makes the code easier to handle downstream, since
            // we only have to worry about nextAction, nextStream, and ignore, without trying to
            // work out the context, based on where we are in the array.
            if (m_swordtask != null)
            {
                // Check the actions...
                for (swordaction = m_swordtask.GetFirstAction();
                     swordaction != null;
                     swordaction = swordaction.GetNextAction())
                {
                    // Fix the exception...
                    if (swordaction.GetException() == "@nextActionOrIgnore")
                    {
                        // If we're not the last action, set nextAction, else set ignore...
                        swordaction.SetException((swordaction.GetNextAction() != null) ? "nextAction" : "ignore");
                    }

                    // Check encryptionProfiles...
                    if (swordaction.GetAction() == "encryptionProfiles")
                    {
                        // Check the encryptionProfiles...
                        for (swordencryptionprofile = swordaction.GetFirstEncryptionProfile();
                             swordencryptionprofile != null;
                             swordencryptionprofile = swordencryptionprofile.GetNextEncryptionProfile())
                        {
                            // If we're not the last action, set nextStream, else set ignore...
                            string szException = (swordencryptionprofile.GetNextEncryptionProfile() != null) ? "nextEncryptionProfile" : "ignore";

                            // Fix the exception...
                            if (swordencryptionprofile.GetException() == "@nextEncryptionProfileOrIgnore")
                            {
                                swordencryptionprofile.SetException(szException);
                            }
                        }
                    }

                    // Check encryptionReport...
                    else if (swordaction.GetAction() == "encryptionReport")
                    {
                        // There's nothing deeper at this time...
                    }

                    // Everything else comes here, including configure...
                    else
                    {
                        // Check the streams...
                        for (swordstream = swordaction.GetFirstStream();
                             swordstream != null;
                             swordstream = swordstream.GetNextStream())
                        {
                            // If we're not the last action, set nextStream, else set ignore...
                            string szException = (swordstream.GetNextStream() != null) ? "nextStream" : "ignore";

                            // Fix the exception...
                            if (swordstream.GetException() == "@nextStreamOrIgnore")
                            {
                                swordstream.SetException(szException);
                            }

                            // Check the sources...
                            for (swordsource = swordstream.GetFirstSource();
                                 swordsource != null;
                                 swordsource = swordsource.GetNextSource())
                            {
                                // Fix the exception...
                                if (swordsource.GetException() == "@nextStreamOrIgnore")
                                {
                                    swordsource.SetException(szException);
                                }

                                // Check the pixel formats...
                                for (swordpixelformat = swordsource.GetFirstPixelFormat();
                                     swordpixelformat != null;
                                     swordpixelformat = swordpixelformat.GetNextPixelFormat())
                                {
                                    // Fix the exception...
                                    if (swordpixelformat.GetException() == "@nextStreamOrIgnore")
                                    {
                                        swordpixelformat.SetException(szException);
                                    }

                                    // Check the attributes...
                                    for (swordattribute = swordpixelformat.GetFirstAttribute();
                                         swordattribute != null;
                                         swordattribute = swordattribute.GetNextAttribute())
                                    {
                                        // Fix the exception...
                                        if (swordattribute.GetException() == "@nextStreamOrIgnore")
                                        {
                                            swordattribute.SetException(szException);
                                        }

                                        // Check the values...
                                        for (swordvalue = swordattribute.GetFirstValue();
                                             swordvalue != null;
                                             swordvalue = swordvalue.GetNextValue())
                                        {
                                            // Fix the exception...
                                            if (swordvalue.GetException() == "@nextStreamOrIgnore")
                                            {
                                                swordvalue.SetException(szException);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // We're good...
            return (true);
        }

        /// <summary>
        /// Get the driver that we'll be using (this also allows us to
        /// check that we have a driver that we can use).
        /// </summary>
        /// <returns>The currently selected driver or null, if there are no drivers</returns>
        public static string GetCurrentDriver(string a_szWriteFolder, string a_szScanner)
        {
            string szTwainDefaultDriver;
            ProcessSwordTask processswordtask;

            // Create the SWORD manager...
            processswordtask = new ProcessSwordTask("", null, null);

            // Check for a TWAIN driver...
            szTwainDefaultDriver = processswordtask.TwainGetDefaultDriver(a_szScanner);

            // Cleanup...
            processswordtask.Close();
            processswordtask = null;
            return (szTwainDefaultDriver);
        }

        /// <summary>
        /// Get information about the TWAIN driver...
        /// </summary>
        /// <returns></returns>
        public DeviceRegister GetDeviceRegister()
        {
            return (m_deviceregister);
        }

        /// <summary>
        /// Return the selected encryption profile name or null...
        /// </summary>
        /// <returns>encryption profile or null</returns>
        public string GetEncryptionProfileName()
        {
            if (string.IsNullOrEmpty(m_szEncryptionProfileName))
            {
                return (null);
            }
            return (m_szEncryptionProfileName);
        }

        /// <summary>
        /// Get the JSON lookup object for this task...
        /// </summary>
        /// <returns>the object or null</returns>
        public JsonLookup GetJsonlookupTask()
        {
            return (m_jsonlookupTask);
        }

        /// <summary>
        /// Get our SWORD Task Response object...
        /// </summary>
        /// <returns>the object</returns>
        public SwordTaskResponse GetSwordTaskResponse()
        {
            return (m_swordtaskresponse);
        }

        /// <summary>
        /// Get the task reply...
        /// </summary>
        /// <returns></returns>
        public string GetTaskReply()
        {
            return (m_swordtaskresponse.GetTaskResponse());
        }

        /// <summary>
        /// This is for use by TWAIN drivers that support DAT_TWAINDIRECT,
        /// so they can return their task reply...
        /// </summary>
        public void SetTaskReply(string a_szTaskReply)
        {
            m_swordtaskresponse.SetTaskResponse(a_szTaskReply);
        }

        /// <summary>
        /// Process, run and return the result from this task...
        /// </summary>
        /// <param name="m_configurenamelookup">info for stream, source, and pixelFormat names</param>
        /// <param name="a_szEncryptionProfileName">name of a profile or null</param>
        /// <returns>true on success</returns>
        public bool ProcessAndRun
        (
            out ConfigureNameLookup a_configurenamelookup,
            out string a_szEncryptionProfileName
        )
        {
            bool blSuccess;
            SwordStatus swordstatus;
            SwordAction swordaction;

            // Init stuff, don't remember any prior encryption profile...
            a_configurenamelookup = null;
            a_szEncryptionProfileName = null;
            SetEncryptionProfileName(null);

            // If we don't have a task or an action, we're done, we return true
            // because this is a null task...
            if ((m_swordtask == null) || (m_swordtask.GetFirstAction() == null))
            {
                m_swordtaskresponse.JSON_ROOT_BGN();                        // {
                m_swordtaskresponse.JSON_ARR_BGN(1, "actions");             // actions:[
                m_swordtaskresponse.JSON_OBJ_BGN(2, "");                    // {
                m_swordtaskresponse.JSON_STR_SET(3, "action", ",", "");     // action:"",
                m_swordtaskresponse.JSON_OBJ_BGN(3, "results");             // results:{
                m_swordtaskresponse.JSON_TOK_SET(4, "success", "", "true"); // success:true
                m_swordtaskresponse.JSON_OBJ_END(3, "");                    // }
                m_swordtaskresponse.JSON_OBJ_END(2, "");                    // }
                m_swordtaskresponse.JSON_ARR_END(1, "");                    // ]
                m_swordtaskresponse.JSON_ROOT_END();                        // }
                return (true);
            }

            // Invoke processing for the list of actions, note that during this part
            // of the function we won't communicate with the scanner.  None of that
            // happens until we hit Run...
            for (swordaction = m_swordtask.GetFirstAction();
                 swordaction != null;
                 swordaction = swordaction.GetNextAction())
            {
                // Figure out our action...
                swordstatus = swordaction.Process();
                switch (swordstatus)
                {
                    default:
                    case SwordStatus.Fail:
                        m_swordtask.BuildTaskReply();
                        return (false);
                    case SwordStatus.NextAction:
                        break;
                    case SwordStatus.VendorMismatch:
                        break;
                    case SwordStatus.Run:
                    case SwordStatus.Success:
                    case SwordStatus.SuccessIgnore:
                        break;
                }
            }

            // Run the actions...
            for (swordaction = m_swordtask.GetFirstAction();
                 swordaction != null;
                 swordaction = swordaction.GetNextAction())
            {
                // Figure out our action...
                swordstatus = Run(swordaction);
                switch (swordstatus)
                {
                    default:
                    case SwordStatus.Fail:
                        m_swordtask.BuildTaskReply();
                        return (false);
                    case SwordStatus.NextAction:
                        break;
                    case SwordStatus.VendorMismatch:
                        break;
                    case SwordStatus.Success:
                    case SwordStatus.SuccessIgnore:
                        break;
                }
            }

            // Build the TWAIN Direct reply...
            blSuccess = m_swordtask.BuildTaskReply();

            // Squirrel away the encryptionProfile, if we got one...
            a_szEncryptionProfileName = GetEncryptionProfileName();

            // All done...
            return (blSuccess);
        }

        /// <summary>
        /// Are we processing an action?
        /// </summary>
        /// <returns>true if we're still processing an action</returns>
        public bool IsProcessing()
        {
            return (m_blProcessing);
        }

        /// <summary>
        /// There's probably a better way of doing this...
        /// </summary>
        /// <returns></returns>
        public int[] Resolution()
        {
            return (m_aiResolution);
        }

        /// <summary>
        /// Return both names and information about the TWAIN drivers installed on the
        /// system, which means that we have to open them to ask questions.  And that
        /// means that the scanner has to be turned on if we're going to be able to talk
        /// to it.
        /// 
        /// This is our one chance to filter out potential problem drivers.  We're not
        /// being too aggressive about this, since a user can always remove a driver
        /// that's not behaving well.
        /// 
        /// The worst scenerio we can run into is a driver that behaves badly (hangs or
        /// crashes) just talking to it, since that can prevent access to other better
        /// behaved drivers.  However, this is a problem for TWAIN CS to solve, not for
        /// TWAIN Direct on TWAIN, so focus any efforts there instead of here.
        /// </summary>
        /// <param name="a_szTwainListAction">getproductnames, getinquiry</param>
        /// <param name="a_szTwainListData">data for the action, like a scanner name for inquiry</param>
        /// <returns>the list of drivers</returns>
        public static string TwainListDrivers(string a_szTwainListAction, string a_szTwainListData)
        {
            string szStatus;
            string szTwainDriverIdentity;
            string szList = "";
            string szTwainListProductName = "";
            IntPtr intptrHwnd;
            TWAINCSToolkit twaincstoolkit;
            TWAIN.STS sts;

            // Get an hwnd...
            intptrHwnd = Interpreter.GetDesktopWindow();

            // Create the toolkit...
            try
            {
                twaincstoolkit = new TWAINCSToolkit
                (
                    intptrHwnd,
                    null,
                    null,
                    null,
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    3,
                    new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                    "USA",
                    "testing...",
                    "ENGLISH_USA",
                    1,
                    0,
                    false,
                    true,
                    (TWAINCSToolkit.RunInUiThreadDelegate)null,
                    null
                );
            }
            catch
            {
                twaincstoolkit = null;
                return (null);
            }

            // If the action is "namesonly" then package up what we have...
            if (!string.IsNullOrEmpty(a_szTwainListAction) && (a_szTwainListAction == "getinquiry"))
            {
                szTwainListProductName = a_szTwainListData;
            }

            // Cycle through the drivers and build up a list of identities...
            int iIndex = -1;
            string[] aszTwidentity = new string[256];
            szStatus = "";
            szTwainDriverIdentity = "";
            for (sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_GETFIRST", ref szTwainDriverIdentity, ref szStatus);
                 sts == TWAIN.STS.SUCCESS;
                 sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_GETNEXT", ref szTwainDriverIdentity, ref szStatus))
            {
                // If we're doing an inquiry, only pass on the requested item...
                if (!string.IsNullOrEmpty(szTwainListProductName))
                {
                    string[] szTwidentityBits = CSV.Parse(szTwainDriverIdentity);
                    if (szTwainListProductName != szTwidentityBits[11]) // TW_IDENTITY.ProductName
                    {
                        continue;
                    }
                }

                // Save this identity...
                iIndex += 1;
                aszTwidentity[iIndex] = szTwainDriverIdentity;

                // Prep for the next entry...
                szStatus = "";
                szTwainDriverIdentity = "";

                // In inquiry mode, we can scoot if we get this far...
                if (!string.IsNullOrEmpty(szTwainListProductName))
                {
                    break;
                }
            }

            // If the action is "getproductnames" then package up what we have...
            if (!string.IsNullOrEmpty(a_szTwainListAction) && (a_szTwainListAction == "getproductnames"))
            {
                // If we have nothing, return an empty string...
                if (aszTwidentity.Length == 0)
                {
                    return ("");
                }

                // Otherwise, build a list of scanner names...
                szList = "{\"scanners\":[";
                for (int ii = 0; (ii < aszTwidentity.Length) && !string.IsNullOrEmpty(aszTwidentity[ii]); ii++)
                {
                    string[] aszTwidentityBits = CSV.Parse(aszTwidentity[ii]);
                    if (ii > 0)
                    {
                        szList += ",";
                    }
                    szList += "{";
                    szList += "\"twidentityProductName\":\"" + aszTwidentityBits[11] + "\""; // TW_IDENTITY.ProductName
                    szList += "}";
                }
                szList += "]}";

                // Destroy the toolkit...
                twaincstoolkit.Cleanup();
                twaincstoolkit = null;

                // Return the list...
                return (szList);
            }

            // Okay, we have a list of identities, so now let's try to open each one of
            // them up and ask some questions...
            szList = "{\"scanners\":[";
            string szTwidentityLast = null;
            foreach (string szTwidentity in aszTwidentity)
            {
                DeviceRegister.TwainInquiryData twaininquirydata;

                // Closing the previous driver up here helps make the code in this section a
                // little cleaner, allowing us to continue instead of having to do a cleanup
                // run in each statement that deals with a problem...
                if (szTwidentityLast != null)
                {
                    szStatus = "";
                    sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref szTwidentityLast, ref szStatus);
                    szTwidentityLast = null;
                    twaincstoolkit.ReopenDSM();
                }
                if (szTwidentity == null)
                {
                    break;
                }

                // Open the driver...
                szStatus = "";
                szTwidentityLast = szTwidentity;
                try
                {
                    sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_OPENDS", ref szTwidentityLast, ref szStatus);
                }
                catch (Exception exception)
                {
                    TWAINWorkingGroup.Log.Info("Driver threw an exception on open: " + szTwidentity);
                    TWAINWorkingGroup.Log.Info(exception.Message);
                    szTwidentityLast = null;
                    continue;
                }
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("Unable to open driver: " + szTwidentity);
                    szTwidentityLast = null;
                    continue;
                }

                // Build an object to add to the list, this is a scratchpad, so if we
                // have to abandon it mid-way through, it's no problem...
                string[] szTwidentityBits = CSV.Parse(szTwidentity);
                if (szTwidentityBits == null)
                {
                    TWAINWorkingGroup.Log.Info("Unable to parse TW_IDENTITY: " + szTwidentity);
                    continue;
                }

                // Check out the driver to see if we can use this driver, and while
                // we're at it, collect interesting information...
                ProcessSwordTask processswordtask = new ProcessSwordTask("", twaincstoolkit, null);
                twaininquirydata = processswordtask.TwainInquiry(szTwidentity);
                if (twaininquirydata.GetTwainDirectSupport() <= DeviceRegister.TwainDirectSupport.Undefined)
                {
                    TWAINWorkingGroup.Log.Error("TwainInquiry says it can't support this driver: " + szTwidentity);
                    continue;
                }

                // Serialize the object...
                string szObject = (!szList.Contains("twidentity")) ? "" : ",";
                szObject += twaininquirydata.Serialize
                (
                    szTwidentityBits[9],  // TW_IDENTITY.Manufacturer
                    szTwidentityBits[11], // TW_IDENTITY.ProductName
                    // Protocol Major.Minor : Version Major.Minor
                    szTwidentityBits[6] + "." + szTwidentityBits[7] + ":" + szTwidentityBits[0] + "." + szTwidentityBits[1]
                );

                // We got this far, so add the object to the list we're building...
                szList += szObject;
            }
            szList += "]}";

            // Take care of the last close, if we have one...
            if (szTwidentityLast != null)
            {
                szStatus = "";
                sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_CLOSEDS", ref szTwidentityLast, ref szStatus);
                szTwidentityLast = null;
            }

            // We didn't find TWAIN or SANE content...
            if (!szList.Contains("twidentity") && !szList.Contains("sane"))
            {
                szList = "";
            }

            // Destroy the toolkit...
            twaincstoolkit.Cleanup();
            twaincstoolkit = null;

            // All done...
            return (szList);
        }

        /// <summary>
        /// Select a TWAIN driver to scan with...
        /// </summary>
        /// <param name="a_szTwainDriverIdentity">The current driver, in case the user cancels the selection</param>
        /// <returns>The currently selected driver or null, if there are no drivers</returns>
        public static string SelectDriver(string a_szTwainDriverIdentity)
        {
            string szStatus;
            string szTwainDriverIdentity;
            TWAINCSToolkit twaincstoolkit;
            TWAIN.STS sts;

            // Create the toolkit...
            try
            {
                twaincstoolkit = new TWAINCSToolkit
                (
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    3,
                    new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                    "USA",
                    "testing...",
                    "ENGLISH_USA",
                    1,
                    0,
                    false,
                    true,
                    (TWAINCSToolkit.RunInUiThreadDelegate)null,
                    null
                );
            }
            catch
            {
                twaincstoolkit = null;
                return (a_szTwainDriverIdentity);
            }

            // Ask the user to select a default...
            szStatus = "";
            szTwainDriverIdentity = "";
            sts = twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", "MSG_USERSELECT", ref szTwainDriverIdentity, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                szTwainDriverIdentity = a_szTwainDriverIdentity;
            }

            // Destroy the toolkit...
            twaincstoolkit.Cleanup();
            twaincstoolkit = null;

            // All done...
            return (szTwainDriverIdentity);
        }

        /// <summary>
        /// Squirrel away the name of the encryption profile...
        /// </summary>
        /// <param name="a_szEncryptionProfileName">the name of the profile</param>
        public void SetEncryptionProfileName(string a_szEncryptionProfileName)
        {
            m_szEncryptionProfileName = a_szEncryptionProfileName;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// These are all the possible outcomes of TWAIN's attempt to use
        /// the task.  By setting these values we'll be able to go back
        /// and create a task that reflects the settings that were actually
        /// used...
        /// </summary>
        public enum SwordStatus
        {
            Undefined,
            Success,
            SuccessIgnore,
            Fail,
            BadValue,
            NextAction,
            NextEncryptionProfile,
            NextStream,
            Ready,
            Run,
            Unsupported,
            VendorMismatch
        }

        /// <summary>
        /// Tells us the owner of the vendor id...
        /// </summary>
        public enum VendorOwner
        {
            TwainDirect,    // owned by TWAIN Direct
            Twain,          // owned by TWAIN Classic
            Scanner,        // owned by the current scanner
            Unknown         // everybody else
        }

        // List of names we can use to resolve the stream, source,
        // and pixelFormat names we got from a configure task...
        public class ConfigureNameLookup
        {
            /// <summary>
            /// Add a new entry...
            /// </summary>
            /// <param name="a_configurenamelookupRoot">root of the list</param>
            /// <param name="a_szStreamName">name for this stream, if any</param>
            /// <param name="a_szSourceName">name for this source, if any</param>
            /// <param name="a_szPixelFormatName">name for this pixelFormat, if any</param>
            /// <param name="a_szSource">what to match, if any</param>
            /// <param name="a_szPixelFormat">what to match, if any</param>
            public static void Add
            (
                ref ConfigureNameLookup a_configurenamelookupRoot,
                string a_szStreamName,
                string a_szSourceName,
                string a_szPixelFormatName,
                string a_szSource,
                string a_szPixelFormat
            )
            {
                // Figure out where we're pointing this thing...
                if (a_configurenamelookupRoot == null)
                {
                    a_configurenamelookupRoot = new ConfigureNameLookup(a_szStreamName, a_szSourceName, a_szPixelFormatName, a_szSource, a_szPixelFormat);
                }
                else
                {
                    ConfigureNameLookup configurenamelookup;
                    for (configurenamelookup = a_configurenamelookupRoot;
                         configurenamelookup.m_configurenamelookupNext != null;
                         configurenamelookup = configurenamelookup.m_configurenamelookupNext)
                    {
                        // Nothing needed here...
                    }
                    configurenamelookup.m_configurenamelookupNext = new ConfigureNameLookup(a_szStreamName, a_szSourceName, a_szPixelFormatName, a_szSource, a_szPixelFormat);
                }
            }

            /// <summary>
            /// Find the best match, this is always done using the root...
            /// </summary>
            /// <param name="a_szSource"></param>
            /// <param name="a_szPixelFormat"></param>
            /// <returns></returns>
            public ConfigureNameLookup Find
            (
                string a_szSource,
                string a_szPixelFormat
            )
            {
                string szStreamName;
                string szSourceName;
                string szPixelFormatName;
                ConfigureNameLookup configurenamelookup;

                // Loopy...
                for (configurenamelookup = this;
                     configurenamelookup.m_configurenamelookupNext != null;
                     configurenamelookup = configurenamelookup.m_configurenamelookupNext)
                {
                    // What's the verdict?
                    if (    ((string.IsNullOrEmpty(configurenamelookup.m_szSource) || (a_szSource == configurenamelookup.m_szSource))
                        &&  ((string.IsNullOrEmpty(configurenamelookup.m_szPixelFormat) || (a_szPixelFormat == configurenamelookup.m_szPixelFormat)))))
                    {
                        return (configurenamelookup);
                    }
                }

                // Ruh-roh, get what we can...
                szStreamName = configurenamelookup.m_szStreamName;
                if (string.IsNullOrEmpty(szStreamName))
                {
                    szStreamName = "stream0";
                }
                szSourceName = configurenamelookup.m_szSourceName;
                if (string.IsNullOrEmpty(szSourceName))
                {
                    szSourceName = "source0";
                }
                szPixelFormatName = configurenamelookup.m_szPixelFormatName;
                if (string.IsNullOrEmpty(szPixelFormatName))
                {
                    szPixelFormatName = "pixelFormat0";
                }

                // Not ideal, but our best guess...
                return (new ConfigureNameLookup(szStreamName, szSourceName, szPixelFormatName, a_szSource, a_szPixelFormat));
            }

            /// <summary>
            /// The name of this stream...
            /// </summary>
            /// <returns>the name of this stream</returns>
            public string GetStreamName()
            {
                return (m_szStreamName);
            }

            /// <summary>
            /// The name of this source...
            /// </summary>
            /// <returns>the name of this source</returns>
            public string GetSourceName()
            {
                return (m_szSourceName);
            }

            /// <summary>
            /// The name of this pixelFormat...
            /// </summary>
            /// <returns>the name of this pixelFormat</returns>
            public string GetPixelFormatName()
            {
                return (m_szPixelFormatName);
            }

            /// <summary>
            /// This is just for us...
            /// </summary>
            private ConfigureNameLookup
            (
                string a_szStreamName,
                string a_szSourceName,
                string a_szPixelFormatName,
                string a_szSource,
                string a_szPixelFormat
            )
            {
                m_szStreamName = a_szStreamName;
                m_szSourceName = a_szSourceName;
                m_szPixelFormatName = a_szPixelFormatName;
                m_szSource = a_szSource;
                m_szPixelFormat = a_szPixelFormat;
            }

            // The names for this entry...
            private string m_szStreamName;
            private string m_szSourceName;
            private string m_szPixelFormatName;

            // The source and pixelFormat needed for a match...
            private string m_szSource;
            private string m_szPixelFormat;

            // Next entry or null...
            private ConfigureNameLookup m_configurenamelookupNext;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// Cleanup...
        /// </summary>
        /// <param name="a_blDisposing">true if we need to clean up managed resources</param>
        internal void Dispose(bool a_blDisposing)
        {
            if (a_blDisposing)
            {
                // We have a toolkit...
                if (m_twaincstoolkit != null)
                {
                    // Only call if it doesn't belong to our caller...
                    if (m_twaincstoolkitCaller == null)
                    {
                        m_twaincstoolkit.Dispose();
                    }

                    // Make sure our reference counts drop...
                    m_twaincstoolkit = null;
                }

                // Null this out too...
                m_twaincstoolkitCaller = null;
            }
        }

        /// <summary>
        /// We want to make sure that tasks follow a strict topology order, this
        /// means that terms in that topology cannot appear out of sequence...
        /// </summary>
        /// <param name="a_swordaction">the action where we'll record any problems</param>
        /// <param name="a_szKey">the current key in the topology (ex: action, stream)</param>
        /// <param name="a_szKey">the current path to the key</param>
        /// <returns></returns>
        private bool CheckTopology(SwordAction a_swordaction, string a_szKey, string a_szPath)
        {
            string szFullKey;
            SwordTaskResponse swordtaskresponse = a_swordaction.GetSwordTaskResponse();

            // If this is custom to a vendor, then skip this test, we can't
            // know what they're doing, so we shouldn't try to check it...
            szFullKey = a_szPath + ((a_szPath != "") ? ".vendor" : "vendor");
            if (GetVendorOwner(m_jsonlookupTask.Get(szFullKey,false)) == VendorOwner.Unknown)
            {
                return (true);
            }

            // If we find actions, but it's not an actions array, we have a problem...
            if (a_szKey != "actions")
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".actions" : "actions");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: actions");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find action, but it's not an action key or a streams array, we have a problem...
            if ((a_szKey != "action") && (a_szKey != "streams") && (a_szKey != "encryptionProfiles"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".action" : "action");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: action");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find streams, but it's not an action key or a streams array, we have a problem...
            if ((a_szKey != "streams") && (a_szKey != "action"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".streams" : "streams");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: streams");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find stream, but it's not a stream key or a sources array, we have a problem...
            if ((a_szKey != "stream") && (a_szKey != "sources"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".stream" : "stream");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: stream");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find sources, but it's not a stream key or a sources array, we have a problem...
            if ((a_szKey != "sources") && (a_szKey != "stream"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".sources" : "sources");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: sources");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find source, but it's not a source key or a pixelformats array, we have a problem...
            if ((a_szKey != "source") && (a_szKey != "pixelFormats"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".source" : "source");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: source");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // // If we find pixelformats, but it's not a source key or a pixelformats array, we have a problem...
            if ((a_szKey != "pixelFormats") && (a_szKey != "source"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".pixelFormats" : "pixelFormats");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: pixelFormats");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find pixelformat, but it's not a pixelformat key or an attributes array, we have a problem...
            if ((a_szKey != "pixelFormat") && (a_szKey != "attributes"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".pixelFormat" : "pixelFormat");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: pixelFormat");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find attributes, but it's not a pixelformat key or an attributes array, we have a problem...
            if ((a_szKey != "attributes") && (a_szKey != "pixelFormat"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".attributes" : "attributes");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: attributes");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find attribute, but it's not an attribute key or a value array, we have a problem...
            if ((a_szKey != "attribute") && (a_szKey != "values"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".attribute" : "attribute");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: attribute");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find values, but it's not an attribute key or a value array, we have a problem...
            if ((a_szKey != "values") && (a_szKey != "attribute"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".values" : "values");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: values");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // If we find value, but it's not a value key, we have a problem...
            if ((a_szKey != "value"))
            {
                szFullKey = a_szPath + ((a_szPath != "") ? ".value" : "value");
                if (!string.IsNullOrEmpty(m_jsonlookupTask.Get(szFullKey,false)))
                {
                    TWAINWorkingGroup.Log.Error("topology violation: value");
                    swordtaskresponse.SetError("fail", szFullKey, "invalidTask", -1);
                    a_swordaction.SetSwordStatus(SwordStatus.Fail);
                    return (false);
                }
            }

            // We're good...
            return (true);
        }

        /// <summary>
        /// Returns the owner of the vendor id...
        /// </summary>
        /// <param name="a_szVendor">vendor id to test</param>
        /// <returns>who owns it</returns>
        public VendorOwner GetVendorOwner(string a_szVendor)
        {
            // If we have no data, we assume TWAIN Direct...
            if (string.IsNullOrEmpty(a_szVendor))
            {
                return (VendorOwner.TwainDirect);
            }

            // The TWAIN Direct vendor...
            if (a_szVendor == m_szVendorTwainDirect)
            {
                return (VendorOwner.TwainDirect);
            }

            // The scanner's vendor...
            if (a_szVendor == m_szVendor)
            {
                return (VendorOwner.Scanner);
            }

            // The escape clause...
            return (VendorOwner.Unknown);
        }

        /// <summary>
        /// Load the twainlist info, we only do this if we need
        /// to, and we only do it once per session.
        /// </summary>
        /// <returns>true if we found info</returns>
        private bool LoadTwainListInfo()
        {
            int ii;
            int jj;
            bool blSuccess;
            long lJsonErrorIndex;
            string szScanner;
            string szTwainlist;
            string szTwidentityProductName;
            JsonLookup jsonlookup;

            // We've already done this...
            if (m_jsonlookupTwidentity != null)
            {
                return (true);
            }

            // We're going to need help for this next bit, if we can't find
            // the twainlist file, we're done...
            szTwainlist = Config.Get("twainlist", null);
            if (string.IsNullOrEmpty(szTwainlist) || !File.Exists(szTwainlist))
            {
                TWAINWorkingGroup.Log.Error("We don't have a twainlist...");
                return (false);
            }

            // Read it...
            try
            {
                szTwainlist = File.ReadAllText(szTwainlist);
            }
            catch
            {
                TWAINWorkingGroup.Log.Error("Failed to read twainlist...");
                return (false);
            }

            // Load and parse the data...
            jsonlookup = new JsonLookup();
            blSuccess = jsonlookup.Load(szTwainlist, out lJsonErrorIndex);
            if (!blSuccess)
            {
                TWAINWorkingGroup.Log.Error("Failed to load twainlist...");
                return (false);
            }

            // Find our scanner...
            szTwidentityProductName = "";
            szScanner = Config.Get("scanner", null);
            for (ii = 0 ;; ii++)
            {
                // Get the next scanner object from the scanners array...
                szTwidentityProductName = jsonlookup.Get("scanners[" + ii + "].twidentityProductName", false);
                if (string.IsNullOrEmpty(szTwidentityProductName))
                {
                    break;
                }

                // Bail if we have a match...
                if (szTwidentityProductName == szScanner)
                {
                    break;
                }
            }

            // No joy...
            if (string.IsNullOrEmpty(szTwidentityProductName))
            {
                return (false);
            }

            // Load just this info...
            m_jsonlookupTwidentity = new JsonLookup();
            blSuccess = m_jsonlookupTwidentity.Load(jsonlookup.Get("scanners[" + ii + "]", false), out lJsonErrorIndex);
            if (!blSuccess)
            {
                TWAINWorkingGroup.Log.Error("Failed to load twainlist...");
                return (false);
            }

            // Get then data, then resize the array...
            m_aiResolution = new int[1024];
            for (jj = ii = 0 ;; ii++)
            {
                string szResolution = m_jsonlookupTwidentity.Get("resolution[" + ii + "]", false);
                if (string.IsNullOrEmpty(szResolution))
                {
                    break;
                }
                // Only numbers...
                if (int.TryParse(szResolution, out m_aiResolution[jj]))
                {
                    // Only positive numbers...
                    if (m_aiResolution[jj] > 0)
                    {
                        jj += 1;
                    }
                }
            }
            if (jj == 0)
            {
                m_aiResolution = null;
            }
            else
            {
                Array.Resize(ref m_aiResolution, jj);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// When we reset all of the TWAIN capabilities, we really shouldn't be
        /// resetting those caps that belong solely to the application.  However,
        /// the spec doesn't clearly call this out, so to protect ourselves we
        /// have to make sure those values are restored...
        /// </summary>
        /// <param name="a_swordaction">action we're doing</param>
        /// <returns>true on success</returns>
        private bool ResetAll(SwordAction a_swordaction)
        {
            string szStatus;
            string szCapability;
            TWAIN.STS sts;

            // Reset everything, if it's supported...
            if (a_swordaction.GetProcessSwordTask().GetDeviceRegister().GetTwainInquiryData().GetCapabilityResetall())
            {
                szStatus = "";
                szCapability = ""; // don't need valid data for this call...
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_RESETALL", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Process: MSG_RESETALL failed");
                    // well, that stinks...keep going...
                }
            }

            // Memory transfers give us more lattitude to support things
            // like compression.  But we only use it for drivers that
            // are tagged with extended support...
            if (a_swordaction.GetProcessSwordTask().GetDeviceRegister().GetTwainInquiryData().GetTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Extended)
            {
                szStatus = "";
                szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16," + Config.Get("icapXfermech", "2"); // TWSX_MEMORY
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMORY");
                    a_swordaction.SetError("fail", a_swordaction.GetJsonKey() + ".action", "invalidValue", -1);
                    return (false);
                }
            }
            // Otherwise, use native transfers, which are safer with the
            // basic drivers...
            else
            {
                szStatus = "";
                szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,0";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info("Action: we can't set ICAP_XFERMECH to TWSX_MEMORY");
                    a_swordaction.SetError("fail", a_swordaction.GetJsonKey() + ".action", "invalidValue", -1);
                    return (false);
                }
            }

            // No UI, all drivers must support this, unless useCapIndicators is set
            // to false.  If this is done then the driver's UI will be used, which
            // is a bad experience, but may be necessary for some folks.  The line
            // is written so that only 'false' will work...
            if (Config.Get("useCapIndicators", "true") != "false")
            {
                szStatus = "";
                szCapability = "CAP_INDICATORS,TWON_ONEVALUE,TWTY_BOOL,0";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error("Action: we can't set CAP_INDICATORS to FALSE");
                    a_swordaction.SetError("fail", a_swordaction.GetJsonKey() + ".action", "invalidValue", -1);
                    return (false);
                }
            }

            // Ask for extended image info, if we can get it...
            if (a_swordaction.GetProcessSwordTask().GetDeviceRegister().GetTwainInquiryData().GetExtImageInfo())
            {
                szStatus = "";
                szCapability = "ICAP_EXTIMAGEINFO";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if ((sts == TWAIN.STS.SUCCESS) && szStatus.EndsWith("0"))
                {
                    szStatus = "";
                    szCapability = "ICAP_EXTIMAGEINFO,TWON_ONEVALUE,TWTY_BOOL,1";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Warn("Action: we can't set ICAP_EXTIMAGEINFO to TRUE");
                        a_swordaction.SetError("fail", a_swordaction.GetJsonKey() + ".action", "invalidValue", -1);
                        return (false);
                    }
                }
            }

            // If we're a basic scanner, try to turn off compression, we don't
            // care about the result, so don't bother to check...
            if (a_swordaction.GetProcessSwordTask().GetDeviceRegister().GetTwainInquiryData().GetTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Basic)
            {
                szStatus = "";
                szCapability = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,0"; // TWCP_NONE
                m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
            }

            // Life is good...
            return (true);
        }

        /// <summary>
        /// Run the current action with whatever data we collected...
        /// </summary>
        /// <param name="a_swordaction">the action to run</param>
        /// <returns>true on success</returns>
        private SwordStatus Run
        (
            SwordAction a_swordaction
        )
        {
            SwordStatus swordstatus = SwordStatus.Success;
            string szAction;

            // Don't run this action...
            if (a_swordaction.GetSwordStatus() != SwordStatus.Run)
            {
                return (SwordStatus.SuccessIgnore);
            }

            // Get the action...
            szAction = a_swordaction.GetAction();
            if (string.IsNullOrEmpty(szAction))
            {
                szAction = "";
            }

            // Dispatch the action...
            switch (szAction)
            {
                // Ruh-roh...
                default:
                    // Apply our exceptions...
                    switch (a_swordaction.GetException())
                    {
                        default:
                            return (SwordStatus.SuccessIgnore);
                        case "nextAction":
                            a_swordaction.SetError("nextAction", a_swordaction.GetJsonKey() + ".action", "invalidValue", -1);
                            return (SwordStatus.NextAction);
                        case "nextEncryptionProfile":
                            return (SwordStatus.NextEncryptionProfile);
                        case "nextStream":
                            return (SwordStatus.NextStream);
                        case "fail":
                            a_swordaction.SetError("fail", a_swordaction.GetJsonKey() + ".action", "invalidValue", -1);
                            return (SwordStatus.Fail);
                    }

                // Configure...
                case "configure":
                    swordstatus = RunConfigure(a_swordaction);
                    return (swordstatus);

                // Encryption profiles...
                case "encryptionProfiles":
                    swordstatus = RunEncryptionProfiles(a_swordaction);
                    return (swordstatus);

                // Encryption report...
                case "encryptionReport":
                    swordstatus = RunEncryptionReport(a_swordaction);
                    return (swordstatus);
            }
        }

        /// <summary>
        ///	Run the configure action with whatever data we collected,
        ///	this is the point where we need to pay attention to the
        ///	capability ordering, so that the database does the right
        ///	thing...
        /// </summary>
        /// <param name="a_swordaction"></param>
        /// <returns></returns>
        SwordStatus RunConfigure
        (
            SwordAction a_swordaction
        )
        {
            SwordStatus swordstatus;
            SwordStream swordstream;
            SwordSource swordsource;

            ////////////////////////////////////////////////////////////////////
            // Configuring requires us to reset the database back to its
            // power-on defaults.  So let's do that first...
            #region Reset all

            // Clear the configure name lookup list, we'll built this on the fly...
            m_configurenamelookup = null;

            // Reset the scanner.  This won't necessarily work for every device.
            // We're not going to treat it as a failure, though, because the user
            // should be able to get a factory default experience from their driver
            // in other ways.  Like from the driver's GUI.
            ResetAll(a_swordaction);

            #endregion

            ////////////////////////////////////////////////////////////////////
            // Handle a configure topology that's empty...
            #region Check for empties

            // If we don't have a stream (which is fine) then we're done...
            swordstream = a_swordaction.GetFirstStream();
            if (swordstream == null)
            {
                a_swordaction.SetSwordStatus(SwordStatus.Success);
                return (SwordStatus.Success);
            }

            // If we don't have a source in our stream, then we're done...
            swordsource = swordstream.GetFirstSource();
            if (swordsource == null)
            {
                swordstream.SetSwordStatus(SwordStatus.Success);
                a_swordaction.SetSwordStatus(SwordStatus.Success);
                return (SwordStatus.Success);
            }

            #endregion

            ////////////////////////////////////////////////////////////////////
            // Walk the streams...
            #region Walk the streams

            // Find the first stream we can try to use...
            swordstatus = SwordStatus.Success;
            for (swordstream = a_swordaction.GetFirstStream();
                    swordstream != null;
                    swordstream = swordstream.GetNextStream())
            {
                // Skip this stream...
                if (swordstream.GetSwordStatus() != SwordStatus.Run)
                {
                    continue;
                }

                // Run the stream...
                swordstatus = RunConfigureStream(swordstream);

                // If the stream is successful, then we're done...
                if (swordstatus == SwordStatus.Success)
                {
                    swordstream.SetSwordStatus(swordstatus);
                    break;
                }

                // If we're not nextstream, then we're done...
                if (swordstatus != SwordStatus.NextStream)
                {
                    a_swordaction.SetSwordStatus(swordstatus);
                    return (swordstatus);
                }

                // We're going to try the next stream, but we
                // need to reset stuff first...
                ResetAll(a_swordaction);
            }

            // We're good...
            a_swordaction.SetSwordStatus(swordstatus);

            #endregion

            ////////////////////////////////////////////////////////////////////
            // If a failure occurs, we must reset the scanner back to its
            // power-on defaults, so that any attempt to scan will always
            // produce the same results...
            #region Reset all (on failure)

            // We ran into a problem...
            if (    (swordstatus != SwordStatus.Success)
                &&  (swordstatus != SwordStatus.SuccessIgnore))
            {
                // Reset the scanner.  This won't necessarily work for every device.
                // We're not going to treat it as a failure, though, because the user
                // should be able to get a factory default experience from their driver
                // in other ways.  Like from the driver's GUI.
                ResetAll(a_swordaction);
            }

            #endregion

            // Return our status...
            return (swordstatus);
        }

        /// <summary>
        ///	Run the configure stream with whatever data we collected,
        ///	this is the point where we need to pay attention to the
        ///	capability ordering, so that the database does the right
        ///	thing...
        /// </summary>
        /// <param name="a_swordstream"></param>
        /// <returns></returns>
        SwordStatus RunConfigureStream
        (
            SwordStream a_swordstream
        )
        {
	        int iPixelformatFlags;
            string szAutomaticSenseMedium;
            string szCapCameraSide;
            string szCapFeederEnabled;
            string szStatus;
            string[] aszCapabilities;
            TWAIN.STS sts;
	        SwordSource swordsource;
	        SwordSource swordsourceFirst;
	        SwordPixelFormat swordpixelformat;
	        SwordPixelFormat swordpixelformatFirst;
	        SwordAttribute swordattribute;
	        SwordValue swordvalue;
            SwordStatus swordstatus;

            // Always start with a clean slate for the name lookup...
            m_configurenamelookup = null;

	        // An empty stream always passes...
	        if (a_swordstream == null)
	        {
		        return (SwordStatus.Success);
	        }

            ////////////////////////////////////////////////////////////////////////////
            // A configuration's topology indicates which cameras are enabled for
            // image capture.  Typically, a user wants all of the cameras to have
            // the same settings.  But in other cases a user wants to pick different
            // behavior for each camera.
            //
            // We support four cameras: bitonal front, bitonal rear, color front and
            // color rear.  Grayscale is treated as an attribute of a color camera.
            //
            // This section maps the task's topology to our database.  We have four
            // basic scenarios:  simple (all cameras the same), different settings
            // for the front / rear or color / bitonal, multi-pixelFormat (which
            // we've historically called dual stream), and automatic color detection.
            //
            // At the end of the function we generate a profile in the log so that
            // it's possible to confirm that what a task asked for really happened...
            ////////////////////////////////////////////////////////////////////////////
            #region Topology and Attributes

            // Assume success...
            swordstatus = SwordStatus.Success;

            // If we don't have a source, make do with what we have...
            if (a_swordstream.GetFirstSource() == null)
            {
                ConfigureNameLookup.Add(ref m_configurenamelookup, a_swordstream.GetName(), "", "", "", "");
            }

            // For each source...
            for (swordsourceFirst = swordsource = a_swordstream.GetFirstSource();
		         swordsource != null;
		         swordsource = swordsource.GetNextSource())
	        {
		        // Skip this source...
		        if (swordsource.GetSwordStatus() != SwordStatus.Run)
		        {
			        continue;
		        }

                // Count the pixelformats for this source...
		        iPixelformatFlags = 0;
                for (swordpixelformatFirst = swordpixelformat = swordsource.GetFirstPixelFormat();
                     swordpixelformat != null;
                     swordpixelformat = swordpixelformat.GetNextPixelFormat())
                {
                    iPixelformatFlags += 1;
                }

                // Get the source settings...
                szAutomaticSenseMedium = swordsource.GetAutomaticSenseMedium();
                szCapCameraSide = swordsource.GetCameraSide();
                szCapFeederEnabled = swordsource.GetFeederEnabled();
                bool blAutomaticSenseMedium = false;

                // Try to do automatic sense medium...
                if (!string.IsNullOrEmpty(szAutomaticSenseMedium))
                {
                    // Try to use the capability...
                    szStatus = "";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szAutomaticSenseMedium, ref szStatus);
                    if (sts == TWAIN.STS.SUCCESS)
                    {
                        blAutomaticSenseMedium = true;
                    }

                    // TBD: If that fails, try to use CAP_FEEDERLOADED with CAP_FEEDERENABLED to work it out...
                }

                // Force the source...
                if (!blAutomaticSenseMedium && !string.IsNullOrEmpty(szCapFeederEnabled))
                {
                    szStatus = "";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapFeederEnabled, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        return (SwordStatus.Fail);
                    }
                }

                // Set the cameraside...
                if (!string.IsNullOrEmpty(szCapCameraSide))
                {
                    szStatus = "";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapCameraSide, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        return (SwordStatus.Fail);
                    }
                }

                // If we don't have a pixelformat, make do with what we have...
                if (swordsource.GetFirstPixelFormat() == null)
                {
                    ConfigureNameLookup.Add(ref m_configurenamelookup, a_swordstream.GetName(), swordsource.GetName(), "", swordsource.GetSource(), "");
                }

                // For each pixelformat...
                for (swordpixelformatFirst = swordpixelformat = swordsource.GetFirstPixelFormat();
                     swordpixelformat != null;
                     swordpixelformat = swordpixelformat.GetNextPixelFormat())
                {
                    // Assume that we'll be ignoring the pixelformat...
                    swordstatus = SwordStatus.SuccessIgnore;

                    // Skip this pixelformat...
                    if (swordpixelformat.GetSwordStatus() != SwordStatus.Run)
                    {
                        continue;
                    }

                    // Record this...
                    ConfigureNameLookup.Add(ref m_configurenamelookup, a_swordstream.GetName(), swordsource.GetName(), swordpixelformat.GetName(), swordsource.GetSource(), swordpixelformat.GetPixelFormat());

                    // Set the pixelformat...
                    string szCapPixelType;
                    string szPixelformt = swordpixelformat.GetPixelFormat();
                    if (string.IsNullOrEmpty(szPixelformt) || (szPixelformt == "any"))
                    {
                        szCapPixelType = "ICAP_PIXELTYPE";
                        szStatus = "";
                        sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapPixelType, ref szStatus);
                        if (sts != TWAIN.STS.SUCCESS)
                        {
                            TWAINWorkingGroup.Log.Error("Couldn't get ICAP_PIXELTYPE for 'any'");
                            return (SwordStatus.Fail);
                        }
                        string[] aszContainer = CSV.Parse(szCapPixelType);
                        if ((aszContainer == null) || (aszContainer.Length < 3) || (aszContainer[1] != "TWON_ONEVALUE"))
                        {
                            TWAINWorkingGroup.Log.Error("Couldn't parse ICAP_PIXELTYPE for 'any'");
                            return (SwordStatus.Fail);
                        }
                        switch (aszContainer[3])
                        {
                            default:
                                TWAINWorkingGroup.Log.Error("Unsupported ICAP_PIXELTYPE for 'any' <" + aszContainer[3] + ">");
                                return (SwordStatus.Fail);
                            case "0":
                                szPixelformt = "bw1";
                                break;
                            case "1":
                                szPixelformt = "gray8";
                                break;
                            case "2":
                                szPixelformt = "rgb24";
                                break;
                        }
                    }
                    switch (szPixelformt)
                    {
                        default:
                            szCapPixelType = "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,-1"; // cause a failure...
                            break;
                        case "bw1":
                            szCapPixelType = "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,0"; // TWPT_BW
                            break;
                        case "gray8":
                            szCapPixelType = "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,1"; // TWPT_GRAY
                            break;
                        case "rgb24":
                            szCapPixelType = "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,2"; // TWPT_RGB24
                            break;
                    }
                    szStatus = "";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapPixelType, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        return (SwordStatus.Fail);
                    }

                    // For each attribute...
                    for (swordattribute = swordpixelformat.GetFirstAttribute();
                         swordattribute != null;
                         swordattribute = swordattribute.GetNextAttribute())
                    {
                        // Assume that we'll be ignoring the attribute...
                        swordstatus = SwordStatus.SuccessIgnore;

                        // Skip this attribute...
                        if (swordattribute.GetSwordStatus() != SwordStatus.Run)
                        {
                            continue;
                        }

                        // For each value...
                        //tbd we need to sort stuff before we get this far...
                        for (swordvalue = swordattribute.GetFirstValue();
                             swordvalue != null;
                             swordvalue = swordvalue.GetNextValue())
                        {
                            // Assume that we'll be ignoring the value...
                            swordstatus = SwordStatus.SuccessIgnore;

                            // Skip this value...
                            if (swordvalue.GetSwordStatus() != SwordStatus.Run)
                            {
                                continue;
                            }

                            // Well that's not good, not sure how we got this far, but
                            // go ahead and ignore it...
                            aszCapabilities = swordvalue.GetCapability();
                            if ((aszCapabilities == null) || (aszCapabilities.Length == 0))
                            {
                                swordvalue.SetSwordStatus(swordstatus);
                                break;
                            }

                            // Some attributes need to set more than one TWAIN capability...
                            string szCapabilityFail = "";
                            sts = TWAIN.STS.SUCCESS;
                            foreach (string sz in aszCapabilities)
                            {
                                // Set the capability, bail on an error...
                                string szCapability = sz;
                                szStatus = "";
                                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                                if (sts != TWAIN.STS.SUCCESS)
                                {
                                    szCapabilityFail = szCapability;
                                    break;
                                }
                            }

                            // We successfully set all the stuff...
                            if (sts == TWAIN.STS.SUCCESS)
                            {
                                swordstatus = SwordStatus.Success;
                                swordvalue.SetSwordStatus(swordstatus);
                            }
                            // Something bad happened, figure out what the user wants us to do...
                            else
                            {
                                switch (swordvalue.GetException())
                                {
                                    default:
                                    case "ignore":
                                        // We want to try the next value, if we have one...
                                        swordstatus = SwordStatus.BadValue;
                                        break;
                                    case "nextAction":
                                        // We're out of here...
                                        swordstatus = SwordStatus.NextAction;
                                        TWAINWorkingGroup.Log.Error("Set error: " + szCapabilityFail + szStatus);
                                        swordvalue.SetError("nextAction", swordvalue.GetJsonKey() + ".value", "invalidValue", -1);
                                        break;
                                    case "nextStream":
                                        // We're out of here...
                                        swordstatus = SwordStatus.NextStream;
                                        break;
                                    case "fail":
                                        // Whoops, time to empty the pool...
                                        swordstatus = SwordStatus.Fail;
                                        TWAINWorkingGroup.Log.Error("Set error: " + szCapabilityFail + szStatus);
                                        swordvalue.SetError("fail", swordvalue.GetJsonKey() + ".value", "invalidValue", -1);
                                        break;
                                }
                            }

                            // Make a note of this...
                            swordvalue.SetSwordStatus(swordstatus);

                            // Only continue this loop on SuccessIgnore or BadValue...
                            if (    (swordstatus != SwordStatus.SuccessIgnore)
                                &&  (swordstatus != SwordStatus.BadValue))
                            {
                                break;
                            }
                        } // END: For each value...

                        // How did we do with the value?
                        switch (swordstatus)
                        {
                            // Pass the result up...
                            default:
                            case SwordStatus.Success:
                            case SwordStatus.Fail:
                            case SwordStatus.NextStream:
                                swordattribute.SetSwordStatus(swordstatus);
                                break;

                            // Make it successful at this level...
                            case SwordStatus.SuccessIgnore:
                                swordstatus = SwordStatus.Success;
                                swordattribute.SetSwordStatus(swordstatus);
                                break;

                            // If we got a bad value, we'll register it as
                            // Success.  Remember that failure at this level
                            // occurs when we can't verify the existence of
                            // a capability.
                            case SwordStatus.BadValue:
                                // TBD: add code to try to figure out if we failed
                                // because of a bad value, or because one of the
                                // capabilities isn't supported.  This could be done
                                // by creating a string array of the capabilities
                                // that we try in the values loop, comparing that to
                                // the supported caps array...
                                swordstatus = SwordStatus.Success;
                                swordattribute.SetSwordStatus(swordstatus);
                                break;
                        }

                        // Only continue this loop on SuccessIgnore or Success...
                        if (    (swordstatus != SwordStatus.SuccessIgnore)
                            &&  (swordstatus != SwordStatus.Success))
                        {
                            break;
                        }
                    } // END: For each attribute...

                    // How did we do with the attribute?
                    switch (swordstatus)
                    {
                        // Pass the result up...
                        default:
                        case SwordStatus.Success:
                        case SwordStatus.Fail:
                        case SwordStatus.NextStream:
                            swordpixelformat.SetSwordStatus(swordstatus);
                            break;

                        // Make it successful at this level...
                        case SwordStatus.SuccessIgnore:
                            swordstatus = SwordStatus.Success;
                            swordpixelformat.SetSwordStatus(swordstatus);
                            break;

                        // If we got a bad value, we'll register it as
                        // Success.  Remember that failure at this level
                        // occurs when we can't verify the existence of
                        // a capability needed for a pixelformat...
                        case SwordStatus.BadValue:
                            swordstatus = SwordStatus.Success;
                            swordpixelformat.SetSwordStatus(swordstatus);
                            break;
                    }

                    // Only continue this loop on SuccessIgnore or Success...
                    if (    (swordstatus != SwordStatus.SuccessIgnore)
                        &&  (swordstatus != SwordStatus.Success))
                    {
                        break;
                    }
                } // END: For each pixelformat...

                // How did we do with the pixelformat?
                switch (swordstatus)
                {
                    // Pass the result up...
                    default:
                    case SwordStatus.Success:
                    case SwordStatus.Fail:
                    case SwordStatus.NextStream:
                        swordsource.SetSwordStatus(swordstatus);
                        break;

                    // Make it successful at this level...
                    case SwordStatus.SuccessIgnore:
                        swordstatus = SwordStatus.Success;
                        swordsource.SetSwordStatus(swordstatus);
                        break;

                    // If we got a bad value, we'll register it as
                    // Success.  Remember that failure at this level
                    // occurs when we can't verify the existence of
                    // a capability needed for a pixelformat...
                    case SwordStatus.BadValue:
                        swordstatus = SwordStatus.Success;
                        swordsource.SetSwordStatus(swordstatus);
                        break;
                }

                // Only continue this loop on SuccessIgnore or Success...
                if (    (swordstatus != SwordStatus.SuccessIgnore)
                    &&  (swordstatus != SwordStatus.Success))
                {
                    break;
                }
            } // END: For each source...

            // How did we do with the source?
            switch (swordstatus)
            {
                // Pass the result up...
                default:
                case SwordStatus.Success:
                case SwordStatus.Fail:
                case SwordStatus.NextStream:
                    a_swordstream.SetSwordStatus(swordstatus);
                    break;

                // Make it successful at this level...
                case SwordStatus.SuccessIgnore:
                    swordstatus = SwordStatus.Success;
                    a_swordstream.SetSwordStatus(swordstatus);
                    break;

                // If we got a bad value, we'll register it as
                // Success.  Remember that failure at this level
                // occurs when we can't verify the existence of
                // a capability needed for a pixelformat...
                case SwordStatus.BadValue:
                    swordstatus = SwordStatus.Success;
                    a_swordstream.SetSwordStatus(swordstatus);
                    break;
            }

            #endregion

            // All done...
            return (swordstatus);
        }

        /// <summary>
        ///	Run the encryption profiles action with whatever data we collected...
        /// </summary>
        /// <param name="a_swordaction"></param>
        /// <returns></returns>
        SwordStatus RunEncryptionProfiles
        (
            SwordAction a_swordaction
        )
        {
            SwordStatus swordstatus;
            SwordEncryptionProfile swordencryptionprofile;

            ////////////////////////////////////////////////////////////////////
            // Walk the encryptionProfiles...
            #region Walk the encryptionProfiles

            // Find the first encryptionProfile we can try to use...
            swordstatus = SwordStatus.Success;
            for (swordencryptionprofile = a_swordaction.GetFirstEncryptionProfile();
                 swordencryptionprofile != null;
                 swordencryptionprofile = swordencryptionprofile.GetNextEncryptionProfile())
            {
                bool blFound;
                int iProfile;

                // Skip this encryptionProfile...
                if (swordencryptionprofile.GetSwordStatus() != SwordStatus.Run)
                {
                    continue;
                }

                // Check our config to see what we have...
                iProfile = 0;
                blFound = false;
                while (true)
                {
                    // Get the next encryptionProfile...
                    string szProfileName = Config.Get("encryptionProfiles[" + iProfile + "].name", "");
                    if (string.IsNullOrEmpty(szProfileName))
                    {
                        break;
                    }

                    // We match it...
                    if (swordencryptionprofile.GetName() == szProfileName)
                    {
                        blFound = true;
                        break;
                    }

                    // Look for another...
                    iProfile += 1;
                }

                // If we recognize the profile, then use it...
                if (blFound)
                {
                    swordstatus = SwordStatus.Success;
                }
                else if (swordencryptionprofile.GetException() == "fail")
                {
                    swordstatus = SwordStatus.Fail;
                }
                else if (swordencryptionprofile.GetException() == "nextEncryptionProfile")
                {
                    swordstatus = SwordStatus.NextEncryptionProfile;
                }
                else if (swordencryptionprofile.GetException() == "nextAction")
                {
                    swordstatus = SwordStatus.NextAction;
                }
                else
                {
                    continue;
                }

                // If the stream is successful, then we're done...
                if (swordstatus == SwordStatus.Success)
                {
                    swordencryptionprofile.SetSwordStatus(swordstatus);
                    a_swordaction.GetProcessSwordTask().SetEncryptionProfileName(swordencryptionprofile.GetName());
                    break;
                }

                // If we're not nextencryptionprofile, then we're done...
                if (swordstatus != SwordStatus.NextEncryptionProfile)
                {
                    swordencryptionprofile.SetSwordStatus(swordstatus);
                    a_swordaction.SetSwordStatus(swordstatus);
                    return (swordstatus);
                }
            }

            // We're good...
            a_swordaction.SetSwordStatus(swordstatus);

            #endregion

            // Return our status...
            return (SwordStatus.Success);
        }

        /// <summary>
        ///	Run the encryption report action...
        /// </summary>
        /// <param name="a_swordaction"></param>
        /// <returns></returns>
        SwordStatus RunEncryptionReport
        (
            SwordAction a_swordaction
        )
        {
            // Create our report...
            if (CreateEncryptionReport(a_swordaction.GetProcessSwordTask(), a_swordaction) != "success")
            {
                TWAINWorkingGroup.Log.Error("Action: CreateEncryptionReport failed");
                return (SwordStatus.Fail);
            }

            // Return our status...
            return (SwordStatus.Success);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // TWAIN Private methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region TWAIN Private methods...
        /// <summary>
        /// Collect information about the TWAIN Driver.  We need this to decide if we
        /// can safely use it, and to determine what features it can handle.
        /// </summary>
        /// <param name="a_szTwidentity">the driver we're examining</param>
        /// <returns>info about what we learned</returns>
        private DeviceRegister.TwainInquiryData TwainInquiry(string a_szTwidentity)
        {
            int iEnum;
            string szValues;
            string szStatus;
            string szCapability;
            string[] aszContainer;
            string szFunction = "TwainInquiry: ";
            TWAIN.STS sts;

            // Give a clue where we are...
            TWAINWorkingGroup.Log.Info(" ");
            TWAINWorkingGroup.Log.Info(szFunction + "begin...");

            // Start a new object, assume support is none, unless told otherwise...
            m_twaininquirydata = new DeviceRegister.TwainInquiryData();
            m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);

            // I'm using the loop so I can control code flow with break
            // statements.  This section checks for Driver support, meaning
            // that the TWAIN driver advertises all of the functionality
            // needed to process TWAIN Direct tasks, generate TWAIN Direct
            // metadata and PDF/raster images, and provide sufficient control
            // to allow things like stopping the feeder (if it has a feeder).
            //
            // The goal is to get by with the fewest number of tests needed
            // to implement the TWAIN Local interface.  Right now that pans
            // out as follows:
            //
            //      createSession           - n/a
            //      getSession              - n/a
            //      waitForEvents           - n/a
            //      sendTask                - DG_CONTROL/DAT_TWAINDIRECT/MSG_SETTASK
            //      startCapturing          - DG_CONTROL/DAT_USERINTERFACE/MSG_ENABLDS:ShowUI=FALSE
            //      readImageBlockMetadata  - TWEI_TWAINDIRECTMETADATA
            //      readImageBlock          - CAP_XFERMECH:TWSX_MEMFILE, ICAP_IMAGEFILEFORMAT:TWFF_PDFRASTER
            //      releaseImageBlocks      - n/a
            //      stopCapturing           - DG_CONTROL/DAT_PENDINGXFERS/MSG_STOPFEEDERS (for ADFs)
            //      closeSession            - DG_CONTROL/DAT_PENDINGXFERS/MSG_RESET
            //
            // There are some additional checks that go with this, such as
            // capabilities indicating that the UI is controllable, and that
            // the driver supports DAT_EXTIMAGEINFO.
            while (true)
            {
                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Is the device online?  If not, support is None...
                #region Is the device online?

                // Get the current value...
                szStatus = "";
                szCapability = "CAP_DEVICEONLINE";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // If we don't find it, that's bad...                
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "CAP_DEVICEONLINE is false or not supported - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    m_twaininquirydata.SetDeviceOnline(false);
                    return (m_twaininquirydata);
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Oh dear...
                if (aszContainer[1] != "TWON_ONEVALUE")
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "CAP_DEVICEONLINE container for MSG_GETCURRENT must be TWON_ONEVALUE, got <" + aszContainer[1] + "> - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    m_twaininquirydata.SetDeviceOnline(false);
                    return (m_twaininquirydata);
                }

                // If the scanner isn't online, then we can't use this driver...
                if ((aszContainer[3] != "1") && (aszContainer[3] != "TRUE"))
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "CAP_DEVICEONLINE isn't TRUE - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    m_twaininquirydata.SetDeviceOnline(false);
                    return (m_twaininquirydata);
                }

                // Good news, everybody...
                m_twaininquirydata.SetDeviceOnline(true);

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Can we turn the UI off?  If not, support is None...
                #region Is the UI controllable?

                // Get the current value...
                szStatus = "";
                szCapability = "CAP_UICONTROLLABLE";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // If we don't find it, that's bad...                
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "CAP_UICONTROLLABLE is false or not supported - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    m_twaininquirydata.SetUiControllable(false);
                    return (m_twaininquirydata);
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Oh dear...
                if (aszContainer[1] != "TWON_ONEVALUE")
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "CAP_UICONTROLLABLE container for MSG_GETCURRENT must be TWON_ONEVALUE, got <" + aszContainer[1] + "> - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    m_twaininquirydata.SetUiControllable(false);
                    return (m_twaininquirydata);
                }

                // If we can't keep the UI off, then we can't use this driver...
                if ((aszContainer[3] != "1") && (aszContainer[3] != "TRUE"))
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "CAP_UICONTROLLABLE isn't TRUE - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    m_twaininquirydata.SetUiControllable(false);
                    return (m_twaininquirydata);
                }

                // Good news, everybody...
                m_twaininquirydata.SetUiControllable(true);

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Check if DAT_TWAINDIRECT is supported, if not try for Extended support...
                #region Can we use DAT_TWAINDIRECT?

                // Does the driver support DAT_TWAINDIRECT?
                szStatus = TwainGetContainer("CAP_SUPPORTEDDATS");
                if (    !string.IsNullOrEmpty(szStatus)
                    &&  (Config.Get("useDatTwaindirect", "yes") == "yes"))
                {
                    try
                    {
                        string[] asz = CSV.Parse(szStatus);
                        if (asz.Length < 5)
                        {
                            TWAINWorkingGroup.Log.Error(szFunction + "CAP_SUPPORTEDDATS has a badly formed container - " + a_szTwidentity);
                            m_twaininquirydata.SetDatTwainDirect(false);
                        }
                        else
                        {
                            int iNumItems;
                            if (int.TryParse(asz[3], out iNumItems))
                            {
                                string szTwainDirect = (((int)TWAIN.DG.CONTROL << 16) + (int)TWAIN.DAT.TWAINDIRECT).ToString();
                                for (int ii = 0; ii < iNumItems; ii++)
                                {
                                    if (asz[3 + ii] == szTwainDirect)
                                    {
                                        m_twaininquirydata.SetDatTwainDirect(true);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "CAP_SUPPORTEDDATS is not supported (exception) - " + a_szTwidentity);
                        m_twaininquirydata.SetDatTwainDirect(false);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Can we get extended image information, if not try for Extended support...
                #region Is there support for DAT_EXTIMAGEINFO?

                // Get the current value...
                szStatus = "";
                szCapability = "ICAP_EXTIMAGEINFO";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // If we don't find it, that's bad...                
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "ICAP_EXTIMAGEINFO is false or not supported - " + a_szTwidentity);
                    m_twaininquirydata.SetExtImageInfo(false);
                }

                // We got something...
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // Oh dear...
                    if (aszContainer[1] != "TWON_ONEVALUE")
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "ICAP_EXTIMAGEINFO container for MSG_GETCURRENT must be TWON_ONEVALUE, got <" + aszContainer[1] + "> - " + a_szTwidentity);
                        m_twaininquirydata.SetExtImageInfo(false);
                    }

                    // If we can't keep the UI off, then we can't use this driver...
                    else if ((aszContainer[3] != "1") && (aszContainer[3] != "TRUE"))
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "ICAP_EXTIMAGEINFO isn't TRUE - " + a_szTwidentity);
                        m_twaininquirydata.SetExtImageInfo(false);
                    }

                    // Good news, everybody...
                    else
                    {
                        m_twaininquirydata.SetExtImageInfo(true);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Does the driver support TWEI_TWAINDIRECTMETADATA, if not try for Extended support...
                #region We need TWEI_TWAINDIRECTMETADATA

                // Does the driver support TWEI_TWAINDIRECTMETADATA?
                szStatus = TwainGetContainer("ICAP_SUPPORTEDEXTIMAGEINFO");
                if (string.IsNullOrEmpty(szStatus))
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "ICAP_SUPPORTEDEXTIMAGEINFO is not supported - " + a_szTwidentity);
                    m_twaininquirydata.SetTweiTwainDirectMetadata(false);
                }

                // So far so good, now look it up...
                else
                {
                    try
                    {
                        string[] asz = CSV.Parse(szStatus);
                        if (asz.Length < 5)
                        {
                            TWAINWorkingGroup.Log.Error(szFunction + "ICAP_SUPPORTEDEXTIMAGEINFO has a badly formed container - " + a_szTwidentity);
                            m_twaininquirydata.SetTweiTwainDirectMetadata(false);
                        }
                        else
                        {
                            int iNumItems;
                            if (int.TryParse(asz[3], out iNumItems))
                            {
                                string szMetadata = ((int)TWAIN.TWEI.TWAINDIRECTMETADATA).ToString();
                                for (int ii = 0; ii < iNumItems; ii++)
                                {
                                    if (asz[3 + ii] == szMetadata)
                                    {
                                        m_twaininquirydata.SetTweiTwainDirectMetadata(true);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "ICAP_SUPPORTEDEXTIMAGEINFO is not supported (exception) - " + a_szTwidentity);
                        m_twaininquirydata.SetTweiTwainDirectMetadata(false);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Does the driver support TWSX_MEMFILE, if not try for Extended support...
                #region We need TWSX_MEMFILE support

                // Memory file transfer...
                szStatus = "";
                szCapability = "ICAP_XFERMECH,TWON_ONEVALUE,TWTY_UINT16,4"; // TWSX_MEMFILE
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "ICAP_XFERMECH does not support TWSX_MEMFILE - " + a_szTwidentity);
                    m_twaininquirydata.SetImageMemFileXfer(false);
                }

                // We're good...
                else
                {
                    m_twaininquirydata.SetImageMemFileXfer(true);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Does the driver support TWFF_PDFRASTER, if not try for Extended support...
                #region We need TWFF_PDFRASTER support

                // PDF/raster...
                szStatus = "";
                szCapability = "ICAP_IMAGEFILEFORMAT,TWON_ONEVALUE,TWTY_UINT16,17"; // TWFF_PDFRASTER
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "ICAP_IMAGEFILEFORMAT does not support TWFF_PDFRASTER - " + a_szTwidentity);
                    m_twaininquirydata.SetPdfRaster(false);
                }

                // We're good...
                else
                {
                    m_twaininquirydata.SetPdfRaster(true);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Check for DG_CONTROL / DAT_PENDINGXFERS / MSG_RESET, so that we can
                // abort scanning at any time.  Strictly speaking we would need to perform
                // an actual scan to test this.  Instead we're issuing the command, and
                // expect to get back a TWRC_FAILURE/TWCC_SEQERROR indicating that the
                // command is supported, but not in state 4...
                #region We need DG_CONTROL / DAT_PENDINGXFERS / MSG_RESET support

                // MSG_RESET...
                szStatus = "";
                string szPendingXfers = "0,0";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_RESET", ref szPendingXfers, ref szStatus);
                if (sts != TWAIN.STS.SEQERROR)
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "DG_CONTROL/DAT_PENDINGXFERS/MSG_RESET not supported - " + a_szTwidentity);
                    m_twaininquirydata.SetPendingXfersReset(false);
                }

                // We're good...
                else
                {
                    m_twaininquirydata.SetPendingXfersReset(true);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Switch to an ADF.  If this fails we'll skip checks for MSG_STOPFEEDER
                // and CAP_PAPERDETECTABLE.  If it succeeds, then these items are required
                // for use to proceed.  If it doesn't succeed then we're assuming that we
                // have a flatbed, and these capabilities have no meaning...
                #region Switch to ADF for more tests

                // CAP_FEEDERENABLED...
                szStatus = "";
                szCapability = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    m_twaininquirydata.SetPendingXfersStopFeeder(false);
                    m_twaininquirydata.SetPaperDetectable(false);
                }
                else
                {
                    ////////////////////////////////////////////////////////////////////////////////////////////
                    // Check for DG_CONTROL / DAT_PENDINGXFERS / MSG_STOPFEEDER, we need this to
                    // support stopCapturing...
                    #region MSG_STOPFEEDER support

                    szStatus = "";
                    szPendingXfers = "0,0";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_PENDINGXFERS", "MSG_STOPFEEDER", ref szPendingXfers, ref szStatus);
                    if (sts != TWAIN.STS.SEQERROR)
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "DG_CONTROL/DAT_PENDINGXFERS/MSG_STOPFEEDER not supported - " + a_szTwidentity);
                        m_twaininquirydata.SetPendingXfersStopFeeder(false);
                    }

                    // We're good...
                    else
                    {
                        m_twaininquirydata.SetPendingXfersStopFeeder(true);
                    }

                    #endregion


                    ////////////////////////////////////////////////////////////////////////////////////////////
                    // Can we detect paper?  This is a placeholder for now,
                    // ideally we shouldn't need it if a driver supports
                    // TWRC_FAILURE/TWCC_NOMEDIA, but we have no way of testing
                    // for that at this time...
                    #region Is paper detectable?

                    // Get the current value...
                    szStatus = "";
                    szCapability = "CAP_PAPERDETECTABLE";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                    // If we don't find it, that's bad...                
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "CAP_PAPERDETECTABLE is false or not supported - " + a_szTwidentity);
                        m_twaininquirydata.SetPaperDetectable(false);
                    }

                    // We found it...
                    else
                    {
                        // Parse it...
                        aszContainer = CSV.Parse(szCapability);

                        // Oh dear...
                        if (aszContainer[1] != "TWON_ONEVALUE")
                        {
                            TWAINWorkingGroup.Log.Error(szFunction + "CAP_PAPERDETECTABLE container for MSG_GETCURRENT must be TWON_ONEVALUE, got <" + aszContainer[1] + "> - " + a_szTwidentity);
                            m_twaininquirydata.SetPaperDetectable(false);
                        }

                        // Okay, now look for a value...
                        else
                        {
                            // We seeketh truth...
                            if ((aszContainer[3] != "1") && (aszContainer[3] != "TRUE"))
                            {
                                TWAINWorkingGroup.Log.Error(szFunction + "CAP_PAPERDETECTABLE isn't TRUE - " + a_szTwidentity);
                                m_twaininquirydata.SetPaperDetectable(false);
                            }

                            // Good news, everybody...
                            else
                            {
                                m_twaininquirydata.SetPaperDetectable(true);
                            }
                        }
                    }

                    #endregion
                }

                #endregion


                // Congratulations, this is enough for Driver support...
                if (   m_twaininquirydata.GetDeviceOnline()
                    && m_twaininquirydata.GetUiControllable()
                    && m_twaininquirydata.GetDatTwainDirect()
                    && m_twaininquirydata.GetExtImageInfo()
                    && m_twaininquirydata.GetTweiTwainDirectMetadata()
                    && m_twaininquirydata.GetImageMemFileXfer()
                    && m_twaininquirydata.GetPdfRaster()
                    && m_twaininquirydata.GetPendingXfersReset())
                {
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.Driver);
                }


                // Break out of the "loop" and move on...
                break;
            }

            // Collect additional information about the scanner, again, we're using
            // the loop for flow control.  We do this, even for Driver support, in
            // case the user has to manually change the support level inside of the
            // register.txt file.  They're have a complete record of what we know
            // about the TWAIN driver...
            while (true)
            {
                ////////////////////////////////////////////////////////////////////////////////////////////////
                // Make sure the units is set to inches, otherwise we're going to have a less than
                // plesant experience getting information about the cropping region...
                #region Is units set to inches?

                // Get the current value...
                szStatus = "";
                szCapability = "ICAP_UNITS";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // If we don't find it, that's bad, but probably not fatal...                
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_UNITS not supported - " + a_szTwidentity);
                }

                // Keep going...
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // Oh dear...
                    if (aszContainer[1] != "TWON_ONEVALUE")
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "ICAP_UNITS container for MSG_GETCURRENT must be TWON_ONEVALUE - " + a_szTwidentity);
                    }

                    // If we't not inches, then set us to inches...
                    else
                    {
                        if ((aszContainer[3] != "0") && (aszContainer[3] != "TWUN_INCHES"))
                        {
                            szStatus = "";
                            szCapability = aszContainer[0] + "," + aszContainer[1] + "," + aszContainer[2] + ",0";
                            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                            if (sts != TWAIN.STS.SUCCESS)
                            {
                                TWAINWorkingGroup.Log.Info(szFunction + "ICAP_UNITS set failed - " + a_szTwidentity);
                            }
                        }
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // serialNumber: this allows us to uniquely identify a scanner, it's debatable if we
                // need both the hostname and the serial number, but for now that's what we're doing...
                #region serialNumber...

                // Get the current value...
                szStatus = "";
                szCapability = "CAP_SERIALNUMBER";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);

                // It's an error, because we've lost the ability to handle more than one
                // model of this scanner, but it's not fatal, because we can stil handle
                // at least one scanner...                
                if (sts != TWAIN.STS.SUCCESS)
                {
                    // If we don't have a serial number, use X, we need the smallest
                    // value we can get to work with the "TWAIN_FreeImage Software Scanner"...
                    TWAINWorkingGroup.Log.Info(szFunction + "CAP_SERIALNUMBER error - " + a_szTwidentity);
                    m_twaininquirydata.SetSerialNumber("X");
                }

                // Keep on keeping on...
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // We've been weirded out...
                    if (aszContainer[1] != "TWON_ONEVALUE")
                    {
                        TWAINWorkingGroup.Log.Error(szFunction + "CAP_SERIALNUMBER unsupported container - " + a_szTwidentity);
                        m_twaininquirydata.SetSerialNumber("X");
                    }

                    // This is enough to add the item...
                    else
                    {
                        m_twaininquirydata.SetSerialNumber(aszContainer[3]);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // sources...
                #region sources...

                // Get the enumeration...
                szStatus = "";
                szCapability = "CAP_FEEDERENABLED";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);

                // Assume that we have a flatbed...
                if (sts != TWAIN.STS.SUCCESS)
                {
                    m_twaininquirydata.SetFeederDetected(false);
                    m_twaininquirydata.SetFlatbedDetected(true);
                }

                // It looks like we have something else...
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // Handle the container...
                    switch (aszContainer[1])
                    {
                        // Assume flatbed...
                        default:
                            TWAINWorkingGroup.Log.Info(szFunction + "CAP_FEEDERENABLED unsupported container - " + a_szTwidentity);
                            m_twaininquirydata.SetFeederDetected(false);
                            m_twaininquirydata.SetFlatbedDetected(true);
                            break;

                        // These containers are just off by an index, so we can combine them...
                        case "TWON_ONEVALUE":
                        case "TWON_ENUMERATION":
                            m_twaininquirydata.SetFeederDetected(false);
                            m_twaininquirydata.SetFlatbedDetected(false);
                            for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                            {
                                switch (aszContainer[iEnum])
                                {
                                    default:
                                        break;
                                    case "0": // FALSE
                                        m_twaininquirydata.SetFlatbedDetected(true);
                                        break;
                                    case "1": // TRUE
                                        m_twaininquirydata.SetFeederDetected(true);
                                        break;
                                }
                            }
                            break;
                    }

                    // If all else fails, assume flatbed...
                    if (!m_twaininquirydata.GetFlatbedDetected() && !m_twaininquirydata.GetFeederDetected())
                    {
                        m_twaininquirydata.SetFlatbedDetected(true);
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // cap_automaticsensemedium...
                #region cap_automaticsensemedium...

                // Just see if we can get it...
                szStatus = "";
                szCapability = "CAP_AUTOMATICSENSEMEDIUM";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                m_twaininquirydata.SetAutomaticSenseMedium(sts == TWAIN.STS.SUCCESS);

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // cap_cameraside...
                #region cap_cameraside...

                // If we don't have an ADF, we can't take sides... :)
                if (!m_twaininquirydata.GetFeederDetected())
                {
                    m_twaininquirydata.SetCameraSides("[]");
                }

                // Okay, see what we have...
                else
                {
                    // Assume we have nothing...
                    m_twaininquirydata.SetCameraSides("[]");

                    // Get the enumeration...
                    szStatus = "";
                    szCapability = "CAP_CAMERASIDE";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);

                    // It looks like we have something else...
                    if (sts == TWAIN.STS.SUCCESS)
                    {
                        // Parse it...
                        aszContainer = CSV.Parse(szCapability);

                        // Handle the container...
                        switch (aszContainer[1])
                        {
                            // Assume flatbed...
                            default:
                                TWAINWorkingGroup.Log.Info(szFunction + "CAP_CAMERASIDE unsupported container - " + a_szTwidentity);
                                break;

                            // These containers are just off by an index, so we can combine them...
                            case "TWON_ONEVALUE":
                            case "TWON_ENUMERATION":
                                string szArray = "";
                                for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                                {
                                    switch (aszContainer[iEnum])
                                    {
                                        default:
                                            break;
                                        case "0": // BOTH
                                            szArray += (szArray == "") ? "0" : ",0";
                                            break;
                                        case "1": // TOP
                                            szArray += (szArray == "") ? "1" : ",1";
                                            break;
                                        case "2": // BOTTOM
                                            szArray += (szArray == "") ? "2" : ",2";
                                            break;
                                    }
                                }
                                m_twaininquirydata.SetCameraSides("[" + szArray + "]");
                                break;
                        }
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // numberOfSheets...
                #region numberOfSheets...

                // Just see if we can get it...
                szStatus = "";
                szCapability = "CAP_SHEETCOUNT";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                m_twaininquirydata.SetSheetCount(sts == TWAIN.STS.SUCCESS);

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // resolutions...
                #region resolutions...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_XRESOLUTION";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_XRESOLUTION error - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Handle the container...
                szValues = "";
                switch (aszContainer[1])
                {
                    default:
                        TWAINWorkingGroup.Log.Info(szFunction + "ICAP_XRESOLUTION unsupported container - " + a_szTwidentity);
                        m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                        return (m_twaininquirydata);

                    // These containers are just off by an index, so we can combine them.
                    // We should be checking the bitdepth, just to be sure, but this is a
                    // real edge case that shouldn't matter 99% of the time...
                    case "TWON_ONEVALUE":
                    case "TWON_ENUMERATION":
                        for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                        {
                            if (!szValues.Contains(aszContainer[iEnum]))
                            {
                                szValues += (szValues == "") ? aszContainer[iEnum] : ("," + aszContainer[iEnum]);
                            }
                        }
                        break;

                    // We're not going to support ranges at this time.  Instead we'll
                    // pare the range down to a set of commonly used resolutions...
                    case "TWON_RANGE":
                        // Get the min and the max, and add items in that range...
                        double dfMin;
                        double dfMax;
                        if (!double.TryParse(aszContainer[3], out dfMin))
                        {
                            TWAINWorkingGroup.Log.Info(szFunction + "ICAP_XRESOLUTION min error - " + a_szTwidentity);
                            TWAINWorkingGroup.Log.Info(szCapability);
                            m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                            return (m_twaininquirydata);
                        }
                        if (!double.TryParse(aszContainer[4], out dfMax))
                        {
                            TWAINWorkingGroup.Log.Info(szFunction + "ICAP_XRESOLUTION max error - " + a_szTwidentity);
                            TWAINWorkingGroup.Log.Info(szCapability);
                            m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                            return (m_twaininquirydata);
                        }
                        szValues += Convert.ToDecimal(dfMin);
                        foreach (double dfRes in new double[] { 75, 100, 150, 200, 240, 250, 300, 400, 500, 600, 1200, 2400, 4800, 9600, 19200 })
                        {
                            if ((dfMin < dfRes) && (dfRes < dfMax))
                            {
                                szValues += "," + Convert.ToDecimal(dfRes);
                            }
                        }
                        szValues += "," + Convert.ToDecimal(dfMax);
                        break;
                }

                // Add to the list...
                if (string.IsNullOrEmpty(szValues))
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_XRESOLUTION no recognized values - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }

                // Okay, we got something...
                m_twaininquirydata.SetResolutions("[" + szValues + "]");

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // height...
                #region height...

                // Get the physical height...
                szStatus = "";
                szCapability = "ICAP_PHYSICALHEIGHT";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_PHYSICALHEIGHT error - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }
                aszContainer = CSV.Parse(szCapability);
                int iMaxHeightMicrons = (int)(double.Parse(aszContainer[3]) * 25400);

                // Get the physical width...
                szStatus = "";
                szCapability = "ICAP_PHYSICALWIDTH";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_PHYSICALWIDTH error - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }
                aszContainer = CSV.Parse(szCapability);
                int iMaxWidthMicrons = (int)(double.Parse(aszContainer[3]) * 25400);

                // Get the minimum height...
                int iMinHeightMicrons = 0;
                szStatus = "";
                szCapability = "ICAP_MINIMUMHEIGHT";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_MINIMUMHEIGHT not found, we'll use 2 inches - " + a_szTwidentity);
                    iMinHeightMicrons = (2 * 25400);
                }
                else
                {
                    aszContainer = CSV.Parse(szCapability);
                    iMinHeightMicrons = (int)(double.Parse(aszContainer[3]) * 25400);
                }

                // Get the minimum width...
                int iMinWidthMicrons = 0;
                szStatus = "";
                szCapability = "ICAP_MINIMUMWIDTH";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_MINIMUMWIDTH error, we'll use 2 inches - " + a_szTwidentity);
                    iMinWidthMicrons = (2 * 25400);
                }
                else
                {
                    aszContainer = CSV.Parse(szCapability);
                    iMinWidthMicrons = (int)(double.Parse(aszContainer[3]) * 25400);
                }

                // Okay, squirrel it away...
                m_twaininquirydata.SetHeight("[" + iMinHeightMicrons + "," + iMaxHeightMicrons + "]");

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // width...
                #region width...

                m_twaininquirydata.SetWidth("[" + iMinWidthMicrons + "," + iMaxWidthMicrons + "]");

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // offsetX...
                #region offsetX...

                m_twaininquirydata.SetOffsetX("[0," + (iMaxWidthMicrons - iMinWidthMicrons) + "]");

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // offsetY...
                #region offsetY...

                m_twaininquirydata.SetOffsetY("[0," + (iMaxHeightMicrons - iMinHeightMicrons) + "]");

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // cropping...
                #region cropping...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_AUTOMATICBORDERDETECTION";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    m_twaininquirydata.SetCroppings("[\"fixed\"]");
                }
                else
                {
                    // Parse it...
                    aszContainer = CSV.Parse(szCapability);

                    // Handle the container...
                    szValues = "";
                    switch (aszContainer[1])
                    {
                        default:
                            TWAINWorkingGroup.Log.Info(szFunction + "ICAP_AUTOMATICBORDERDETECTION unsupported container - " + a_szTwidentity);
                            continue;

                        // These containers are just off by an index, so we can combine them.
                        // We should be checking the bitdepth, just to be sure, but this is a
                        // real edge case that shouldn't matter 99% of the time...
                        case "TWON_ONEVALUE":
                        case "TWON_ENUMERATION":
                            for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                            {
                                switch (aszContainer[iEnum])
                                {
                                    default:
                                        break;
                                    case "0": // FALSE
                                        if (string.IsNullOrEmpty(m_twaininquirydata.GetCroppings()))
                                        {
                                            m_twaininquirydata.SetCroppings("[\"fixed\"]");
                                        }
                                        else if (m_twaininquirydata.GetCroppings().Contains("auto"))
                                        {
                                            m_twaininquirydata.SetCroppings("[\"auto\",\"fixed\"]");
                                        }
                                        break;
                                    case "1": // TRUE
                                        if (string.IsNullOrEmpty(m_twaininquirydata.GetCroppings()))
                                        {
                                            m_twaininquirydata.SetCroppings("[\"auto\"]");
                                        }
                                        else if (m_twaininquirydata.GetCroppings().Contains("fixed"))
                                        {
                                            m_twaininquirydata.SetCroppings("[\"auto\",\"fixed\"]");
                                        }
                                        break;
                                }
                            }
                            break;
                    }

                    // Add to the list...
                    if (string.IsNullOrEmpty(m_twaininquirydata.GetCroppings()))
                    {
                        TWAINWorkingGroup.Log.Info(szFunction + "ICAP_AUTOMATICBORDERDETECTION no recognized values - " + a_szTwidentity);
                        m_twaininquirydata.SetCroppings("[\"fixed\"]");
                    }
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // pixelFormat...
                #region pixelFormat...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_PIXELTYPE";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_PIXELTYPE error - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Handle the container...
                szValues = "";
                switch (aszContainer[1])
                {
                    default:
                        TWAINWorkingGroup.Log.Info(szFunction + "ICAP_PIXELTYPE unsupported container - " + a_szTwidentity);
                        continue;

                    // These containers are just off by an index, so we can combine them.
                    // We should be checking the bitdepth, just to be sure, but this is a
                    // real edge case that shouldn't matter 99% of the time...
                    case "TWON_ONEVALUE":
                    case "TWON_ENUMERATION":
                        for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                        {
                            switch (aszContainer[iEnum])
                            {
                                default:
                                    break;
                                case "0": // TWPT_BW
                                    if (!szValues.Contains("bw1"))
                                    {
                                        szValues += (szValues == "") ? "\"bw1\"" : ",\"bw1\"";
                                    }
                                    break;
                                case "1": // TW_PT_GRAY
                                    if (!szValues.Contains("gray8"))
                                    {
                                        szValues += (szValues == "") ? "\"gray8\"" : ",\"gray8\"";
                                    }
                                    break;
                                case "2": // TWPT_RGB
                                    if (!szValues.Contains("rgb24"))
                                    {
                                        szValues += (szValues == "") ? "\"rgb24\"" : ",\"rgb24\"";
                                    }
                                    break;
                            }
                        }
                        break;
                }

                // Add to the list...
                if (szValues != "")
                {
                    m_twaininquirydata.SetPixelFormats("[" + szValues + "]");
                }
                else
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_PIXELTYPE no recognized values - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }

                #endregion


                ////////////////////////////////////////////////////////////////////////////////////////////////
                // compression...
                #region compression...

                // Get the enumeration...
                szStatus = "";
                szCapability = "ICAP_COMPRESSION";
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
                if (sts != TWAIN.STS.SUCCESS)
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_COMPRESSION error - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }

                // Parse it...
                aszContainer = CSV.Parse(szCapability);

                // Handle the container...
                szValues = "";
                switch (aszContainer[1])
                {
                    default:
                        TWAINWorkingGroup.Log.Info(szFunction + "ICAP_COMPRESSION unsupported container - " + a_szTwidentity);
                        m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                        return (m_twaininquirydata);

                    // These containers are just off by an index, so we can combine them...
                    case "TWON_ONEVALUE":
                    case "TWON_ENUMERATION":
                        for (iEnum = (aszContainer[1] == "TWON_ONEVALUE") ? 3 : 6; iEnum < aszContainer.Length; iEnum++)
                        {
                            switch (aszContainer[iEnum])
                            {
                                default:
                                    break;
                                case "0": // TWCP_NONE
                                    if (!szValues.Contains("none"))
                                    {
                                        szValues += (szValues == "") ? "\"none\"" : ",\"none\"";
                                    }
                                    break;
                                case "5": // TWCP_GROUP4
                                case "6": // TWCP_JPEG
                                    if (!szValues.Contains("autoVersion1"))
                                    {
                                        szValues += (szValues == "") ? "\"autoVersion1\"" : ",\"autoVersion1\"";
                                    }
                                    break;
                            }
                        }
                        break;
                }

                // Add to the list...
                if (szValues != "")
                {
                    m_twaininquirydata.SetCompressions("[" + szValues + "]");
                }
                else
                {
                    TWAINWorkingGroup.Log.Info(szFunction + "ICAP_COMPRESSION no recognized values - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.None);
                    return (m_twaininquirydata);
                }

                #endregion


                // Good news, we've made it this far, so break out of
                // the "loop" and move on...
                break;
            }


            // If we didn't get Driver support, then we need to see if we can get by
            // with a plan B where TWAIN Bridge handles the TWAIN Direct stuff...
            if (m_twaininquirydata.GetTwainDirectSupport() != DeviceRegister.TwainDirectSupport.Driver)
            {
                // Start by assuming we have Extended support...
                m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.Extended);

                // Reset the scanner, this is required for Extended support, without it
                // we're going to be at a Basic level...
                szStatus = "";
                szCapability = ""; // don't need valid data for this call...
                sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_RESETALL", ref szCapability, ref szStatus);
                if (sts == TWAIN.STS.SUCCESS)
                {
                    m_twaininquirydata.SetCapabilityResetall(true);
                }
                else
                {
                    TWAINWorkingGroup.Log.Error(szFunction + "MSG_RESETALL is not supported - " + a_szTwidentity);
                    m_twaininquirydata.SetTwainDirectSupport(DeviceRegister.TwainDirectSupport.Basic);
                    m_twaininquirydata.SetCapabilityResetall(false);
                }

                // Do we have a vendor ID?
                szStatus = TwainGetValue("CAP_CUSTOMINTERFACEGUID");
                try
                {
                    string[] asz = CSV.Parse(szStatus);
                    string szGuid = asz[asz.Length - 1];
                    m_szVendorOwner = szGuid.Replace("{", "").Replace("}", "");
                }
                catch
                {
                    m_szVendorOwner = "211a1e90-11e1-11e5-9493-1697f925ec7b";
                }

                // Do we have an ADF?
                szStatus = TwainGetValue("CAP_FEEDERENABLED");
                m_blPaperDetectable = ((szStatus != null) && (szStatus == "1"));
                m_twaininquirydata.SetPaperDetectable(m_blPaperDetectable);

                // Can we detect paper?  We don't have to be able to do this,
                // for instance a flatbed has no need of this support, but if
                // we can get it, it's good to know about.
                szStatus = TwainGetValue("CAP_PAPERDETECTABLE");
                m_blPaperDetectable = ((szStatus != null) && (szStatus == "1"));
                m_twaininquirydata.SetPaperDetectable(m_blPaperDetectable);

                // Can we automatically sense the medium?
                szStatus = TwainGetContainer("CAP_AUTOMATICSENSEMEDIUM");
                if (szStatus == null)
                {
                    m_blAutomaticSenseMedium = false;
                }
                else
                {
                    try
                    {
                        string[] asz = CSV.Parse(szStatus);
                        if (asz.Length < 7)
                        {
                            m_blAutomaticSenseMedium = false;
                        }
                        else
                        {
                            for (int ii = 0; ii < int.Parse(asz[3]); ii++)
                            {
                                if (asz[6 + ii] == "1")
                                {
                                    m_blAutomaticSenseMedium = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        m_blAutomaticSenseMedium = false;
                    }
                }

                // Can we detect the source?
                szStatus = TwainGetValue("CAP_FEEDERENABLED");
                m_blFeederEnabled = (szStatus != null);

                // Can we detect color?
                szStatus = TwainGetValue("ICAP_AUTOMATICCOLORENABLED");
                m_blAutomaticColorEnabled = (szStatus != null);
            }

            // All done...
            TWAINWorkingGroup.Log.Info(" ");
            TWAINWorkingGroup.Log.Info("TwainInquiry completed...");

            // All done...
            return (m_twaininquirydata);
        }

        /// <summary>
        /// Pick the stream that we're going to use based on the availability of
        /// the sources and their contingencies. It's easy to imagine this function
        /// getting fairly sophisticated, since there are several possible
        /// configurations and more than one way of identifying them.
        /// </summary>
        /// <param name="a_processswordtask">main object</param>
        /// <param name="a_swordaction">the action we're checking</param>
        /// <returns></returns>
        private string TwainSelectStream
        (
            ProcessSwordTask a_processswordtask,
            SwordAction a_swordaction
        )
        {
            string szStatus;
            SwordStream swordstream;
            SwordSource swordsource;
            SwordPixelFormat swordpixelformat;
            SwordTaskResponse swordtaskresponseStreams;

            // Give a clue where we are...
            TWAINWorkingGroup.Log.Info(" ");
            TWAINWorkingGroup.Log.Info("TwainSelectStream: begin...");

            // We have no streams...
            if (a_swordaction.GetFirstStream() == null)
            {
                ResetAll(a_swordaction);
                TWAINWorkingGroup.Log.Info("TwainSelectStream: default scanning mode (task has no streams)");
                a_processswordtask.m_swordtaskresponse.JSON_OBJ_BGN(2,"");                              //  {
                a_processswordtask.m_swordtaskresponse.JSON_STR_SET(3,"action","","configure");         //      "action":"configure"
                a_processswordtask.m_swordtaskresponse.JSON_OBJ_END(2,",");                             //  }
                return ("success");
            }

            // The first successful stream wins...
            szStatus = "success";
            swordtaskresponseStreams = new SwordTaskResponse(this);
            for (swordstream = a_swordaction.GetFirstStream();
                 swordstream != null;
                 swordstream = swordstream.GetNextStream())
            {
                TWAINWorkingGroup.Log.Info("TwainSelectStream: stream(" + swordstream.GetName() + ")");

                // Start the streams (we can only have one, so setting the array here is fine)...
                swordtaskresponseStreams.JSON_CLEAR();
                swordtaskresponseStreams.JSON_ARR_BGN(3,"streams");                                     //      "streams":[
                swordtaskresponseStreams.JSON_OBJ_BGN(4,"");                                            //          {
                swordtaskresponseStreams.JSON_STR_SET(5,"name",",",swordstream.GetName());              //              "name":"...",
                swordtaskresponseStreams.JSON_ARR_BGN(5,"sources");                                     //              "sources":[

                // Analyze the sources...
                szStatus = "success";
                for (swordsource = swordstream.GetFirstSource();
                     swordsource != null;
                     swordsource = swordsource.GetNextSource())
                {
                    TWAINWorkingGroup.Log.Info("TwainSelectStream: source(" + swordsource.GetName() + "," + swordsource.GetSource() + ")");

                    // Set the source...
                    string szSource = swordsource.GetSource();
                    szStatus = SetSource(swordsource);
                    if (szStatus == "skip")
                    {
                        TWAINWorkingGroup.Log.Info("TwainSelectStream: source belongs to another vendor, so skipping it");
                        continue;
                    }
                    else if (szStatus != "success")
                    {
                        TWAINWorkingGroup.Log.Info("TwainSelectStream: source exception: " + szStatus);
                        break;
                    }

                    // Uh-oh, no pixelFormat...
                    if (swordsource.GetFirstPixelFormat() == null)
                    {
                        // Add this source...
                        swordtaskresponseStreams.JSON_OBJ_BGN(6,"");                                    //                  {
                        swordtaskresponseStreams.JSON_STR_SET(7,"source","",szSource);                  //                      "source":"..."
                        swordtaskresponseStreams.JSON_OBJ_END(6,",");                                   //                  }

                        // Next source...
                        continue;
                    }

                    // Add this source...
                    swordtaskresponseStreams.JSON_OBJ_BGN(6, "");                                       //                  {
                    swordtaskresponseStreams.JSON_STR_SET(7,"source",",",szSource);                     //                      "source":"...",
                    swordtaskresponseStreams.JSON_ARR_BGN(7,"pixelFormats");                            //                      "pixelformats":[

                    // We can have multiple formats in a source, in which case the scanner
                    // will automatically pick the best match.  This section needs to follow
                    // the Capability Ordering rules detailed in the TWAIN Specification.
                    szStatus = "success";
                    for (swordpixelformat = swordsource.GetFirstPixelFormat();
                         swordpixelformat != null;
                         swordpixelformat = swordpixelformat.GetNextPixelFormat())
                    {
                        TWAINWorkingGroup.Log.Info("TwainSelectStream: pixelFormat(" + swordpixelformat.GetName() + "," + swordpixelformat.GetPixelFormat() + ")");

                        // Pick a color...
                        szStatus = TwainSetValue(swordpixelformat.GetCapabilityPixeltype(), swordtaskresponseStreams);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: pixelFormat exception: " + szStatus);
                            break;
                        }

                        // Resolution...
                        szStatus = TwainSetValue(swordpixelformat.GetCapabilityResolution(), swordtaskresponseStreams);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: resolution exception: " + szStatus);
                            break;
                        }

                        // Compression...
                        szStatus = TwainSetValue(swordpixelformat.GetCapabilityCompression(), swordtaskresponseStreams);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: compression exception: " + szStatus);
                            break;
                        }

                        // Xfercount...
                        szStatus = TwainSetValue(swordpixelformat.GetCapabilityXfercount(), swordtaskresponseStreams);
                        if (szStatus != "success")
                        {
                            TWAINWorkingGroup.Log.Info("TwainSelectStream: xfercount exception: " + szStatus);
                            break;
                        }
                    }

                    // End the attributes section...
                    swordtaskresponseStreams.JSON_ARR_END(7, "");                                       //                      ] - pixelFormats array
                    swordtaskresponseStreams.JSON_OBJ_END(6, "");                                       //                  } - source object

                    // Check the status from the format, and if it's not success pass it on...
                    if (szStatus != "success")
                    {
                        break;
                    }
                }

                // End the sources array...
                swordtaskresponseStreams.JSON_ARR_BGN(5,"");                                            //              ] - sources array

                // We only stay in the stream loop if somebody tells us to stay in the
                // loop, which means we bail on success or all other errors...
                if (szStatus != "nextStream")
                {
                    break;
                }

                // Reset the driver so we start from a clean slate...
                ResetAll(a_swordaction);
            }

            // End the stream object and array...
            swordtaskresponseStreams.JSON_OBJ_END(4,"");                                                //          } - stream object
            swordtaskresponseStreams.JSON_ARR_END(3,"");                                                //      ] - streams array

            // Check the status from the stream, and if it's not success exit from the
            // function with the exception...
            switch (szStatus)
            {
                // Just drop down, we're okay...
                default:
                case "ignore":
                case "success":
                    TWAINWorkingGroup.Log.Info("TwainSelectStream: stream search ended in " + szStatus);
                    break;

                // The task has failed.  "nextStream" results in a failure if it was
                // explicitly set by the task for the last stream.  By default the
                // last stream is going to be "ignore"...
                case "fail":
                case "nextStream":
                    TWAINWorkingGroup.Log.Info("TwainSelectStream: stream search ended in error, " + szStatus);
                    return ("fail");
            }

            // Check the image source, so we can properly report it later in
            // the metadata.  If the feederenabled isn't supported, or if it
            // returns 0 (FALSE), then we're scanning from the flatbed...
            szStatus = TwainGetValue("CAP_FEEDERENABLED");
            if ((szStatus == null) || (szStatus == "0"))
            {
                m_blFlatbed = true;
            }

            // Otherwise...
            else
            {
                // Assume we're scanning from an ADF...
                m_blFlatbed = false;

                // Check the automatic sense medium...
                if (m_blAutomaticSenseMedium)
                {
                    szStatus = TwainGetValue("CAP_AUTOMATICSENSEMEDIUM");
                    if ((szStatus != null) && (szStatus == "1"))
                    {
                        // If we find it, check for paper...
                        szStatus = TwainGetValue("CAP_FEEDERLOADED");
                        if ((szStatus != null) && (szStatus == "0"))
                        {
                            // There's no paper, so it's going to be the flatbed...
                            m_blFlatbed = true;
                        }
                    }
                }
            }
            TWAINWorkingGroup.Log.Info("TwainSelectStream: source is a flatbed, " + m_blFlatbed);

            // Are we duplex?
            m_blDuplex = false;
            if (!m_blFlatbed)
            {
                szStatus = TwainGetValue("CAP_DUPLEXENABLED");
                m_blDuplex = ((szStatus != null) && (szStatus == "1"));
            }

            // We're good...
            TWAINWorkingGroup.Log.Info("TwainSelectStream completed...");
            return ("success");
        }

        /// <summary>
        /// Process an action in a task.
        /// </summary>
        /// <param name="a_szTwainDefaultDriver">The scanner we're going to use</param>
        /// <param name="a_blSetAppCapabilities">set the application capabilities (ex: ICAP_XFERMECH)</param>
        /// <returns></returns>
        private bool Process
        (
            string a_szTwainDefaultDriver,
            ref bool a_blSetAppCapabilities
        )
        {
            bool blSuccess;
            SwordAction swordaction;

            // Make a note of where we are...
            TWAINWorkingGroup.Log.Info("");
            TWAINWorkingGroup.Log.Info("Process begin...");

            // Walk all the actions...
            for (swordaction = m_swordtask.GetFirstAction();
                 swordaction != null;
                 swordaction = swordaction.GetNextAction())
            {
                // Dispatch an action...
                blSuccess = Action(this, swordaction, ref a_blSetAppCapabilities);
                if (!blSuccess)
                {
                    TWAINWorkingGroup.Log.Error("Process: action failed");
                    return (false);
                }
            }

            // All done...
            TWAINWorkingGroup.Log.Info("Process completed...");
            return (true);
        }

        /// <summary>
        /// Initiate an action, this is part of running an action...
        /// </summary>
        /// <param name="a_blError">error flag</param>
        /// <param name="a_swordtask">result of the command</param>
        /// <returns>true on success</returns>
        private bool Action
        (
            ProcessSwordTask a_processswordtask,
            SwordAction a_swordaction,
            ref bool a_blSetAppCapabilities
        )
        {
            TWAINWorkingGroup.Log.Info("");
            TWAINWorkingGroup.Log.Info("Action: " + a_swordaction.GetAction());

            // Init stuff (just to be sure)...
            m_blProcessing = false;

            // Dispatch the action...
            switch (a_swordaction.GetAction())
            {
                // We've got a command that's new to us.  Our default
                // behavior is to keep going.
                default:
                    if (a_swordaction.GetException() == "fail")
                    {
                        TWAINWorkingGroup.Log.Error("Action: unrecognized action...<" + a_swordaction.GetAction() + ">");
                        a_processswordtask.m_swordtaskresponse.SetError("fail", a_swordaction.GetJsonKey() + ".action", a_swordaction.GetAction(), -1);
                        return (false);
                    }
                    TWAINWorkingGroup.Log.Info("Action: unrecognized action...<" + a_swordaction.GetAction() + ">");
                    return (true);

                // Configure...
                case "configure":

                    // Make a note that we successfully set these capabilities, so that
                    // we won't have to do it again when scanning starts...
                    a_blSetAppCapabilities = true;

                    // Pick a stream...
                    if (TwainSelectStream(a_processswordtask, a_swordaction) != "success")
                    {
                        TWAINWorkingGroup.Log.Error("Action: TwainSelectStream failed");
                        return (false);
                    }

                    // We're all done with this command...
                    TWAINWorkingGroup.Log.Info("Action complete: configure");
                    return (true);

                // Encryption profiles...
                case "encryptionProfiles":

                    // Pick an encryptionProfile...
                    if (SelectEncryptionProfile(a_processswordtask, a_swordaction) != "success")
                    {
                        TWAINWorkingGroup.Log.Error("Action: SelectEncryptionProfile failed");
                        return (false);
                    }

                    // We're all done with this command...
                    TWAINWorkingGroup.Log.Info("Action complete: encryptionProfiles");
                    return (true);

                // Encryption report...
                case "encryptionReport":

                    // No additional work needed at this time...
                    if (CreateEncryptionReport(a_processswordtask, a_swordaction) != "success")
                    {
                        TWAINWorkingGroup.Log.Error("Action: CreateEncryptionReport failed");
                        return (false);
                    }

                    // We're all done with this command...
                    TWAINWorkingGroup.Log.Info("Action complete: encryptionReport");
                    return (true);
            }
        }

        /// <summary>
        /// Get a TWAIN capability value...
        /// </summary>
        /// <param name="a_szName">The name of the capabilty we want to get</param>
        /// <returns></returns>
        private string TwainGetValue(string a_szName)
        {
            string szStatus;
            string szCapability;
            TWAIN.STS sts;

            // Get the value...
            szStatus = "";
            szCapability = a_szName;
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szCapability, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                return (null);
            }

            // Collect the value...
            string[] asz = CSV.Parse(szCapability);
            if ((asz == null) || (asz.Length != 4))
            {
                return (null);
            }

            // All done...
            return (asz[3]);
        }

        /// <summary>
        /// Get a TWAIN capability container...
        /// </summary>
        /// <param name="a_szName">The name of the capability we want to get</param>
        /// <returns></returns>
        private string TwainGetContainer(string a_szName)
        {
            string szStatus;
            string szCapability;
            TWAIN.STS sts;

            // Get the value...
            szStatus = "";
            szCapability = a_szName;
            sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GET", ref szCapability, ref szStatus);
            if (sts != TWAIN.STS.SUCCESS)
            {
                return (null);
            }

            // All done...
            return (szCapability);
        }

        /// <summary>
        /// Set a TWAIN capability value...
        /// </summary>
        /// <param name="a_capability">The stuff we want to set</param>
        /// <param name="a_swordtaskresponse">our response</param>
        /// <returns></returns>
        private string TwainSetValue(Capability a_capability, SwordTaskResponse a_swordtaskresponse)
        {
            int iSwordValue;
            string szStatus;
            string szSwordName;
            string szSwordValue;
            string szTwainValue;

            // We don't have this item...
            if (a_capability == null)
            {
                return ("success");
            }

            // Do the set...
            szStatus = a_capability.SetScanner(m_twaincstoolkit, out szSwordName, out szSwordValue, out szTwainValue, m_szVendor, a_swordtaskresponse);

            // Update the task reply for pixelFormat...
            if (szSwordName == "pixelFormat")
            {
                a_swordtaskresponse.JSON_OBJ_BGN(8,"");                                     //                  {
                a_swordtaskresponse.JSON_STR_SET(9,"pixelFormat",",",szSwordValue);         //                      "pixelFormat":"...",
                a_swordtaskresponse.JSON_ARR_BGN(9,"attributes");                           //                      "attributes":[
            }

            // Update the task reply for all attributes...
            else
            {
                // Begin the attribute...
                a_swordtaskresponse.JSON_OBJ_BGN(10, "");                                   //                          {
                a_swordtaskresponse.JSON_STR_SET(11, "attribute", ",", szSwordName);        //                              "attribute":"...",
                a_swordtaskresponse.JSON_ARR_BGN(11, "values");                             //                              "values":[
                a_swordtaskresponse.JSON_OBJ_BGN(12, "");                                   //                                  {

                // Handle the value...
                switch (szSwordName)
                {
                    // Handle strings...
                    default:
                        a_swordtaskresponse.JSON_STR_SET(13,"value","",szSwordValue);       //                                      "value":"..."
                        break;

                    // Handle integers...
                    case "sheetcount":
                    case "resolution":
                        if (int.TryParse(szSwordValue, out iSwordValue))
                        {
                            iSwordValue = 0;
                        }
                        a_swordtaskresponse.JSON_NUM_SET(13,"value","",iSwordValue);        //                                      "value":#
                        break;
                }

                // End the attribute...
                a_swordtaskresponse.JSON_OBJ_END(12, "");                                   //                                  } - value object
                a_swordtaskresponse.JSON_ARR_END(11, "");                                   //                              ] - values array
                a_swordtaskresponse.JSON_OBJ_END(10, ",");                                  //                          }, - attribute object
            }

            // All done...
            return (szStatus);
        }

        /// <summary>
        /// Get the TWAIN default driver...
        /// </summary>
        /// <param name="a_szScanner">An override that we'll get instead of the default driver</param>
        /// <returns></returns>
        private string TwainGetDefaultDriver(string a_szScanner)
        {
            // Create the toolkit...
            try
            {
                m_twaincstoolkit = new TWAINCSToolkit
                (
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    "TWAIN Working Group",
                    "TWAIN Sharp",
                    "SWORD-on-TWAIN",
                    2,
                    3,
                    new string[] { "DF_APP2", "DG_CONTROL", "DG_IMAGE" },
                    "USA",
                    "testing...",
                    "ENGLISH_USA",
                    1,
                    0,
                    false,
                    true,
                    (TWAINCSToolkit.RunInUiThreadDelegate)null,
                    this
                );
            }
            catch
            {
                TWAINWorkingGroup.Log.Warn("Error creating toolkit...");
                m_twaincstoolkit = null;
                return (null);
            }

            // Get the default driver...
            if (a_szScanner == null)
            {
                m_szTwainDriverIdentity = "";
                if (m_twaincstoolkit.GetDrivers(ref m_szTwainDriverIdentity) == null)
                {
                    TWAINWorkingGroup.Log.Warn("No TWAIN drivers found...");
                    m_szTwainDriverIdentity = null;
                    return (null);
                }
            }

            // Otherwise, look for a match...
            else
            {
                string szStatus;
                string szMsg = "MSG_GETFIRST";
                TWAIN.STS sts;
                while (true)
                {
                    szStatus = "";
                    m_szTwainDriverIdentity = "";
                    sts = m_twaincstoolkit.Send("DG_CONTROL", "DAT_IDENTITY", szMsg, ref m_szTwainDriverIdentity, ref szStatus);
                    if (sts != TWAIN.STS.SUCCESS)
                    {
                        m_szTwainDriverIdentity = "";
                        break;
                    }
                    if (m_szTwainDriverIdentity.EndsWith("," + a_szScanner))
                    {
                        break;
                    }
                    szMsg = "MSG_GETNEXT";
                }
            }

            // Destroy the toolkit...
            m_twaincstoolkit.Cleanup();
            m_twaincstoolkit = null;

            // All done...
            return (m_szTwainDriverIdentity);
        }

        /// <summary>
        /// Set the source...
        /// </summary>
        /// <param name="a_swordsource">the source we're working off of</param>
        private string SetSource(SwordSource a_swordsource)
        {
            // Automatic sense medium...
            m_capabilityAutomaticsensemedium = null;
            if (m_blAutomaticSenseMedium)
            {
                m_capabilityAutomaticsensemedium = new Capability(null, "source", a_swordsource.GetSource(), a_swordsource.GetException(), a_swordsource.GetJsonKey(), a_swordsource.GetVendor());
            }

            // Duplex enabled...
            m_capabilityDuplexenabled = null;
            if (m_blDuplexEnabled)
            {
                m_capabilityDuplexenabled = new Capability(null, "source", a_swordsource.GetSource(), a_swordsource.GetException(), a_swordsource.GetJsonKey(), a_swordsource.GetVendor());
            }

            // Feeder enabled...
            m_capabilityFeederenabled = null;
            if (m_blFeederEnabled)
            {
                m_capabilityFeederenabled = new Capability(null, "source", a_swordsource.GetSource(), a_swordsource.GetException(), a_swordsource.GetJsonKey(), a_swordsource.GetVendor());
            }

            // All done...
            return ("success");
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // TWAIN Private definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region TWAIN Private definitions...

        /// <summary>
        /// Map a TWAIN capability to a SWORD topology...
        /// </summary>
        private class CapabilityMap
        {
            // Sword objects (some can be null)...
            public SwordSource m_swordsource;
            public SwordPixelFormat m_swordpixelformat;
            public SwordAttribute m_swordattribute;
            public SwordValue m_swordvalue;

            // Capability for MSG_SET...
            public string m_szTwainCapability;
        }

        /// <summary>
        /// The TWAIN capabilities in the order that they must be negotiated
        /// with a driver.  The whole list is in here to make it easier to
        /// match against the TWAIN Spec.  Caps that can't be matched by
        /// TWAIN Direct are commented out, unless we need them under the
        /// hood.  Some of these could come back as vendor-specific to TWAIN,
        /// if needed...
        /// </summary>
        private enum CapabilityOrdering
        {
            //********************************
            // Machine settings...

            // Independent (can be called at any time)
            //CAP_ENABLEDSUIONLY,
            //CAP_CUSTOMDSDATA,
            //CAP_INDICATORS,
            //CAP_INDICATORSMODE,
            //CAP_UICONTROLLABLE,
            //CAP_SERIALNUMBER,
            //ICAP_LAMPSTATE,
            //CAP_BATTERYMINUTES,
            //CAP_BATTERYPERCENTAGE,
            //CAP_POWERSUPPLY,
            //ICAP_BITORDER,
            //CAP_DEVICETIMEDATE,
            //CAP_DEVICEEVENT,
            //CAP_CAMERAPREVIEWUI,
            //CAP_POWERSAVETIME,
            ICAP_AUTODISCARDBLANKPAGES,
            //ACAP_XFERMECH,

            // Semi-independent (roots can be called at any time)
            CAP_ALARMS,
                CAP_ALARMVOLUME,

            //CAP_AUTOMATICCAPTURE,
            //    CAP_TIMEBEFOREFIRSTCAPTURE,
            //    CAP_TIMEBETWEENCAPTURES,

            //CAP_XFERCOUNT,
                CAP_SHEETCOUNT,

            // Dependent (all must be in this order)
            //CAP_CUSTOMINTERFACEGUID,
            //CAP_SUPPORTEDDATS,
            //CAP_SUPPORTEDCAPS,
            //CAP_EXTENDEDCAPS,
            //ICAP_SUPPORTEDEXTIMAGEINFO,
            //CAP_LANGUAGE,
            //CAP_DEVICEONLINE,

            ICAP_XFERMECH,
            //    ICAP_TILES,

            ICAP_UNITS,

            CAP_FEEDERENABLED,
                //ICAP_LIGHTPATH,
                //CAP_FILMTYPE,

                //CAP_DUPLEX,
                    CAP_DUPLEXENABLED,
                    ICAP_IMAGEMERGED,
                    ICAP_IMAGEMERGETHRESHOLD,

                CAP_FEEDERORDER,
                CAP_FEEDERALIGNMENT,
                CAP_PAPERHANDLING,
                CAP_FEEDERPOCKET,
                CAP_FEEDERPREP,

                CAP_AUTOFEED,
                    CAP_CLEARPAGE,
                    CAP_FEEDPAGE,
                    CAP_REWINDPAGE,

                CAP_PAPERDETECTABLE,
                    CAP_FEEDERLOADED,

                CAP_AUTOMATICSENSEMEDIUM,
                ICAP_LIGHTSOURCE,
                ICAP_FEEDERTYPE,

                CAP_DOUBLEFEEDDETECTION,
                    CAP_DOUBLEFEEDDETECTIONLENGTH,
                    CAP_DOUBLEFEEDDETECTIONSENSITIVITY,
                    CAP_DOUBLEFEEDDETECTIONRESPONSE,

            CAP_MICRENABLED,

            CAP_PRINTER,
                CAP_PRINTERENABLED,
                    CAP_PRINTERMODE,
                    CAP_PRINTERVERTICALOFFSET,
                    CAP_PRINTERCHARROTATION,
                    CAP_PRINTERFONTSTYLE,
                    CAP_PRINTERSTRING,
                    CAP_PRINTERINDEXLEADCHAR,
                    CAP_PRINTERINDEXNUMDIGITS,
                    CAP_PRINTERINDEXMAXVALUE,
                    CAP_PRINTERINDEXSTEP,
                    CAP_PRINTERINDEX,
                    CAP_PRINTERSUFFIX,
                    CAP_PRINTERINDEXTRIGGER,
                    CAP_PRINTERSTRINGPREVIEW,

            //ICAP_IMAGEDATASET,
            //ICAP_THUMBNAILENABLED,
            //ICAP_XNATIVERESOLUTION,
            //ICAP_YNATIVERESOLUTION,
            //ICAP_PHYSICALWIDTH,
            //ICAP_PHYSICALHEIGHT,
            //ICAP_MINIMUMHEIGHT,
            //ICAP_MINIMUMWIDTH,
            ICAP_COLORMANAGEMENTENABLED,

            //********************************
            // Camera settings...
            CAP_CAMERASIDE,

            ICAP_AUTOMATICCOLORENABLED,
                ICAP_AUTOMATICCOLORNONCOLORPIXELTYPE,

            ICAP_PIXELTYPE,
                ICAP_BITDEPTH,
                    ICAP_XRESOLUTION,
                    ICAP_YRESOLUTION,
                    ICAP_PIXELFLAVOR,
                    ICAP_PLANARCHUNKY,
                    ICAP_BITDEPTHREDUCTION,
                    ICAP_CUSTHALFTONE,
                    ICAP_HALFTONES,
                    ICAP_THRESHOLD,
                    ICAP_IMAGEFILEFORMAT,
                    ICAP_COMPRESSION,
                        //ICAP_BITORDERCODES,
                        //ICAP_CCITTKFACTOR,
                        //ICAP_PIXELFLAVORCODES,
                        //ICAP_TIMEFILL,
                        //ICAP_JPEGPIXELTYPE,
                        //ICAP_JPEGQUALITY,
                        //ICAP_JPEGSUBSAMPLING,

            CAP_CAMERAENABLED,
            CAP_CAMERAORDER,
            ICAP_ICCPROFILE,
            ICAP_XSCALING,
            ICAP_YSCALING,
            ICAP_ZOOMFACTOR,

            ICAP_AUTOBRIGHT,
                ICAP_BRIGHTNESS,

            ICAP_CONTRAST,
            //ICAP_GAMMA,
            //ICAP_HIGHLIGHT,
            //ICAP_SHADOW,
            //ICAP_EXPOSURETIME,
            ICAP_FILTER,
            ICAP_IMAGEFILTER,
            ICAP_NOISEFILTER,

            ICAP_UNDEFINEDIMAGESIZE,
                ICAP_AUTOMATICBORDERDETECTION,
                ICAP_AUTOMATICDESKEW,
                ICAP_AUTOMATICROTATE,
                ICAP_OVERSCAN,
                CAP_SEGMENTED,
                ICAP_AUTOSIZE,
                ICAP_AUTOMATICCROPUSESFRAME,
                ICAP_AUTOMATICLENGTHDETECTION,

            ICAP_SUPPORTEDSIZES,

            ICAP_MAXFRAMES,
                ICAP_FRAMES,

            ICAP_ORIENTATION,
                ICAP_FLIPROTATION,
                ICAP_ROTATION,
                ICAP_MIRROR,

            //CAP_AUTHOR,
            //CAP_CAPTION,
            //CAP_TIMEDATE,
            //ICAP_FLASHUSED,
            //ICAP_FLASHUSED2,

            CAP_AUTOSCAN,
                CAP_REACQUIREALLOWED,
                CAP_MAXBATCHBUFFERS,

            ICAP_EXTIMAGEINFO,
                ICAP_PATCHCODEDETECTIONENABLED,
                    ICAP_PATCHCODESEARCHMODE,
                    ICAP_PATCHCODEMAXRETRIES,
                    ICAP_PATCHCODETIMEOUT,
                    ICAP_SUPPORTEDPATCHCODETYPES,
                        ICAP_PATCHCODEMAXSEARCHPRIORITIES,
                            ICAP_PATCHCODESEARCHPRIORITIES,

                ICAP_BARCODEENABLED,
                    ICAP_BARCODESEARCHMODE,
                    ICAP_BARCODEMAXRETRIES,
                    ICAP_BARCODETIMEOUT,
                    ICAP_SUPPORTEDBARCODETYPES,
                        ICAP_BARCODEMAXSEARCHPRIORITIES,
                            ICAP_BARCODESEARCHPRIORITIES,

            //CAP_ENDORSER,
            CAP_JOBCONTROL,

            // Must be last!
            Length
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // encryptionProfiles Private methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region encryptionProfiles Private methods...

        /// <summary>
        /// Based on the data sent to us, pick an encryptionProfile...
        /// </summary>
        /// <param name="a_processswordtask">main object</param>
        /// <param name="a_swordaction">the action we're checking</param>
        /// <returns></returns>
        private string SelectEncryptionProfile(ProcessSwordTask a_processswordtask, SwordAction a_swordaction)
        {
            // All done...
            return ("fail");
        }

        /// <summary>
        /// Create an encryptionReport...
        /// </summary>
        /// <param name="a_processswordtask">main object</param>
        /// <param name="a_swordaction">the action we're checking</param>
        /// <returns></returns>
        private string CreateEncryptionReport(ProcessSwordTask a_processswordtask, SwordAction a_swordaction)
        {
            string szPfxfile;

            // Give a clue where we are...
            TWAINWorkingGroup.Log.Info(" ");
            TWAINWorkingGroup.Log.Info("CreateEncryptionReport: begin...");

            // Do we have a digital signature file?
            szPfxfile = Config.Get("pfxFile", "");

            // Start the encryption report...
            TWAINWorkingGroup.Log.Info("CreateEncryptionReport: begin...");
            a_processswordtask.m_swordtaskresponse.JSON_OBJ_BGN(2, "");                                         //  {
            a_processswordtask.m_swordtaskresponse.JSON_STR_SET(3, "action", ",", "encryptionReport");          //      "action":"encryptionReport",
            a_processswordtask.m_swordtaskresponse.JSON_OBJ_BGN(4, "encryptionReport");                         //      "encryptionReport":{

            // We have a digital signature...
            if (!string.IsNullOrEmpty(szPfxfile))
            {
                try
                {
                    X509Certificate2 x509certificate2 = new X509Certificate2(szPfxfile);
                    a_processswordtask.m_swordtaskresponse.JSON_ARR_BGN(5, "digitalSignatures");                //          "digitalSignatures":[
                    a_processswordtask.m_swordtaskresponse.JSON_OBJ_BGN(6, "");                                 //              {
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "format", ",", x509certificate2.GetFormat());                                    // "format":"XYZ"
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "issuer", ",", x509certificate2.Issuer);                                         // "issuer":"XYZ",
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "signatureAlgorithm", ",", x509certificate2.SignatureAlgorithm.Value);                  // "signatureAlgorithm":"XYZ"
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "notAfter", ",", x509certificate2.NotAfter.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));     // "notAfter":"XYZ",
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "notBefore", ",", x509certificate2.NotBefore.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));   // "notBefore":"XYZ",
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "serialNumber", ",", x509certificate2.GetSerialNumberString());                  // "serialNumber":"XYZ"
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "subject", ",", x509certificate2.Subject);                                       // "subject":"XYZ",
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "version", "", x509certificate2.Version.ToString());                             // "version":"XYZ"
                    a_processswordtask.m_swordtaskresponse.JSON_OBJ_END(6, "");                                 //              }
                    a_processswordtask.m_swordtaskresponse.JSON_ARR_END(5, ",");                                //          ]
                }
                catch
                {
                    // Just keep going...
                }
            }

            // List our encryption profiles...
            if (!string.IsNullOrEmpty(Config.Get("encryptionProfiles[0].name", "")))
            {
                a_processswordtask.m_swordtaskresponse.JSON_ARR_BGN(5, "encryptionProfiles");                   //          "encryptionProfiles":[
                int iProfile = 0;
                while (true)
                {
                    if (string.IsNullOrEmpty(Config.Get("encryptionProfiles[" + iProfile + "].name", "")))
                    {
                        break;
                    }
                    a_processswordtask.m_swordtaskresponse.JSON_OBJ_BGN(6, "");                                 //              {
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "name", ",", Config.Get("encryptionProfiles[" + iProfile + "].name", ""));        //                  "name":"XYZ",
                    a_processswordtask.m_swordtaskresponse.JSON_STR_SET(7, "profile", "", Config.Get("encryptionProfiles[" + iProfile + "].profile", ""));   //                  "profile":"XYZ"
                    a_processswordtask.m_swordtaskresponse.JSON_OBJ_END(6, string.IsNullOrEmpty(Config.Get("encryptionProfiles[" + iProfile + "].name", "")) ? "" : ",");   //              }
                    iProfile += 1;
                }
                a_processswordtask.m_swordtaskresponse.JSON_ARR_END(5, ",");                                    //          ],
            }

            // Provide an empty list to indicate that we support public keys...
            a_processswordtask.m_swordtaskresponse.JSON_ARR_BGN(5, "encryptionPublicKeys");                     //          "encryptionPublicKeys":[
            a_processswordtask.m_swordtaskresponse.JSON_ARR_END(5, "");                                         //          ]


            // End the encryption report...
            a_processswordtask.m_swordtaskresponse.JSON_OBJ_END(4, "");                                         //      }
            a_processswordtask.m_swordtaskresponse.JSON_OBJ_END(2, "");                                         //  }
            return ("success");
        }


    #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Classes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Classes...

        /// <summary>
        /// A TWAIN Direct task...
        /// </summary>
        sealed class SwordTask
            {
                /// <summary>
                /// Our constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">The object to use for responses</param>
                public SwordTask
                (
                    ProcessSwordTask a_processswordtask
                )
                {
                    // Init stuff...
                    m_processswordtask = a_processswordtask;
                    m_swordaction = null;
                }

                /// <summary>
                /// Add an action to the task...
                /// </summary>
                /// <param name="a_szJsonKey">the actions[] path</param>
                /// <param name="a_szAction">the action value</param>
                /// <param name="a_szException">the exception for this action</param>
                /// <param name="a_szVendor">the vendor id (if any) for this action</param>
                /// <returns>the new action object or null</returns>
                public SwordAction AppendAction
                (
	                string a_szJsonKey,
                    string a_szAction,
                    string a_szException,
                    string a_szVendor
                )
                {
                    SwordAction swordaction = null;

                    // Allocate us and init with the info we have...
                    swordaction = new SwordAction(m_processswordtask, m_swordaction, a_szJsonKey, a_szAction, a_szException, a_szVendor);

                    // We're not supported by this scanner, so discard us...
                    if (swordaction.GetSwordStatus() == SwordStatus.VendorMismatch)
                    {
                        return (null);
                    }

                    // Make the first action the head of the list...
                    if (m_swordaction == null)
                    {
                        m_swordaction = swordaction;
                    }

                    // All done...
                    return (swordaction);
                }

                ///////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Get the task reply...
                ///////////////////////////////////////////////////////////////////////////
                public bool BuildTaskReply()
                {
                    bool blSuccess;
                    SwordAction swordaction;
                    SwordTaskResponse swordtaskresponse = m_processswordtask.GetSwordTaskResponse();

                    // Bail if we already have data, this should be error
                    // data, since this is the only function that should
                    // be constructing the response on success...
                    if (!string.IsNullOrEmpty(swordtaskresponse.GetTaskResponse()))
                    {
                        return (true);
                    }

                    // If we don't have any actions, then return an empty action
                    // array with success...
                    if (GetFirstAction() == null)
                    {
                        swordtaskresponse.JSON_OBJ_BGN(0, "");
                        swordtaskresponse.JSON_ARR_BGN(1, "actions");
                        swordtaskresponse.JSON_OBJ_BGN(2, "");
                        swordtaskresponse.JSON_STR_SET(3, "action", ",", "");
                        swordtaskresponse.JSON_OBJ_BGN(3, "results");
                        swordtaskresponse.JSON_TOK_SET(4, "success", "", "true");
                        swordtaskresponse.JSON_OBJ_END(3, ""); // results
                        swordtaskresponse.JSON_OBJ_END(2, ""); // action
                        swordtaskresponse.JSON_ARR_END(1, ""); // actions
                        swordtaskresponse.JSON_OBJ_END(0, ""); // root
                        return (true);
                    }

                    // Start of the root...
                    swordtaskresponse.JSON_OBJ_BGN(0, "");

                    // Start of the actions array...
                    swordtaskresponse.JSON_ARR_BGN(1, "actions");

                    // List our actions...
                    for (swordaction = GetFirstAction();
                         swordaction != null;
                         swordaction = swordaction.GetNextAction())
                    {
                        // List an action...
                        blSuccess = swordaction.BuildTaskReply(swordaction == GetFirstAction());
                        if (!blSuccess)
                        {
                            break;
                        }
                    }

                    // End of the actions array...
                    swordtaskresponse.JSON_ARR_END(1, "");

                    // End of the root...
                    swordtaskresponse.JSON_OBJ_END(0, "");

                    // All done...
                    return (true);
                }

                /// <summary>
                /// The head of the actions list...
                /// </summary>
                /// <returns>the first action or null</returns>
                public SwordAction GetFirstAction()
                {
                    return (m_swordaction);
                }

                /// <summary>
                /// The culture name in the format languagecode2-country/regioncode2.
                /// languagecode2 is a lowercase two-letter code derived from ISO 639-1.
                /// country/regioncode2 is derived from ISO 3166 and usually consists of
                /// two uppercase letters, or a BCP-47 language tag.
                /// </summary>
                /// <returns></returns>
                public string GetLocale()
                {
                    if (string.IsNullOrEmpty(m_szLocale))
                    {
                        return (Thread.CurrentThread.CurrentCulture.Name);
                    }
                    return (m_szLocale);
                }

                /// <summary>
                /// The culture name in the format languagecode2-country/regioncode2.
                /// languagecode2 is a lowercase two-letter code derived from ISO 639-1.
                /// country/regioncode2 is derived from ISO 3166 and usually consists of
                /// two uppercase letters, or a BCP-47 language tag.
                /// </summary>
                /// <returns></returns>
                public bool SetLocale(string a_szLocale)
                {
                    // Skip if we have nothing, we'll default to the current...
                    if (string.IsNullOrEmpty(a_szLocale))
                    {
                        return (true);
                    }
                    // Validate it...
                    try
                    {
                        CultureInfo culture = CultureInfo.GetCultureInfo(a_szLocale);
                    }
                    catch (Exception exception)
                    {
                        TWAINWorkingGroup.Log.Error("SetLocale failed: " + exception.Message);
                        return (false);
                    }
                    // Save it...
                    m_szLocale = a_szLocale;
                    return (true);
                }

                /// <summary>
                /// The object given to us...
                /// </summary>
                private ProcessSwordTask m_processswordtask;

                /// <summary>
                /// The head of the actions list...
                /// </summary>
                private SwordAction m_swordaction;

                /// <summary>
                /// The locale to apply to all actions in this task...
                /// </summary>
                private string m_szLocale;
            }

            /// <summary>
            /// A list of zero or more actions for a task...
            /// </summary>
            sealed class SwordAction
            {
                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Constructor...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordAction
                (
                    ProcessSwordTask a_processswordtask,
                    SwordAction a_swordactionHead,
	                string a_szJsonKey,
                    string a_szAction,
                    string a_szException,
                    string a_szVendor
                )
                {
                    // If the vendor isn't us, then skip it, this isn't subject to exceptions,
                    // so we return right away...
                    m_vendorowner = a_processswordtask.GetVendorOwner(a_szVendor);
	                if (m_vendorowner == VendorOwner.Unknown)
	                {
		                m_swordstatus = SwordStatus.VendorMismatch;
		                return;
	                }

                    // There are two categories of task responses in the system.  There's one
                    // for the entire task that reports problems detected with the TWAIN Direct
                    // language.  Then there are task responses at the level of each of the
                    // actions.  These will be reported back in the task reply.
                    m_swordtaskresponse = new SwordTaskResponse(a_processswordtask);

                    // Init stuff...
                    m_processswordtask = a_processswordtask;
	                m_swordstatus = SwordStatus.Success;
                    m_szJsonKey = a_szJsonKey;
                    m_szException = a_szException;
                    m_szVendor = a_szVendor;
                    m_szAction = a_szAction;

                    // If we didn't get an exception, then assign the nextaction placeholder...
                    if (string.IsNullOrEmpty(m_szException))
                    {
                        m_szException = "@nextActionOrIgnore";
                    }

                    // We're the head of the list...
                    if (a_swordactionHead == null)
	                {
		                // nothing needed...
	                }

	                // We're being appended to the list...
	                else
	                {
		                SwordAction swordactionParent;
		                for (swordactionParent = a_swordactionHead; swordactionParent.m_swordactionNext != null; swordactionParent = swordactionParent.m_swordactionNext) ;
		                swordactionParent.m_swordactionNext = this;

                    }
                }

                /// <summary>
                /// Add a stream to an action...
                /// </summary>
                /// <param name="a_szJsonKey">actions[].streams[] key</param>
                /// <param name="a_szStreamName">name of the stream</param>
                /// <param name="a_szException">exception for this stream</param>
                /// <param name="a_szVendor">vendor id, if any</param>
                /// <returns></returns>
                public SwordStream AppendStream
                (
	                string a_szJsonKey,
                    string a_szStreamName,
                    string a_szException,
                    string a_szVendor
                )
                {
                    SwordStream swordstream;

                    // Allocate a new stream and initialize it...
                    swordstream = new SwordStream(m_processswordtask, m_swordtaskresponse, m_swordstream, a_szJsonKey, a_szStreamName, a_szException, a_szVendor);
                    if (swordstream == null)
                    {
                        return (null);
                    }

                    // We're not supported...
                    if (swordstream.GetSwordStatus() == SwordStatus.VendorMismatch)
                    {
                        swordstream = null;
                        return (null);
                    }

                    // Make us the head of the list...
                    if (m_swordstream == null)
                    {
                        m_swordstream = swordstream;
                    }

                    // All done...
                    return (swordstream);
                }

                /// <summary>
                /// Add an encryptionProfile to an action...
                /// </summary>
                /// <param name="a_szJsonKey">actions[].streams[] key</param>
                /// <param name="a_szEncryptionProfileName">name of the encryptionProfile</param>
                /// <param name="a_szException">exception for this encryptionProfile</param>
                /// <param name="a_szVendor">vendor id, if any</param>
                /// <param name="a_szEncryptionProfileProfile">profile of the encryptionProfile</param>
                /// <returns></returns>
                public SwordEncryptionProfile AppendEncryptionProfile
                (
                    string a_szJsonKey,
                    string a_szEncryptionProfileName,
                    string a_szException,
                    string a_szVendor,
                    string a_szEncryptionProfileProfile
                )
                {
                    SwordEncryptionProfile swordencryptionprofile;

                    // Allocate a new stream and initialize it...
                    swordencryptionprofile = new SwordEncryptionProfile(m_processswordtask, m_swordtaskresponse, m_swordencryptionprofile, a_szJsonKey, a_szEncryptionProfileName, a_szException, a_szVendor, a_szEncryptionProfileProfile);
                    if (swordencryptionprofile == null)
                    {
                        return (null);
                    }

                    // We're not supported...
                    if (swordencryptionprofile.GetSwordStatus() == SwordStatus.VendorMismatch)
                    {
                        swordencryptionprofile = null;
                        return (null);
                    }

                    // Make us the head of the list...
                    if (m_swordencryptionprofile == null)
                    {
                        m_swordencryptionprofile = swordencryptionprofile;
                    }

                    // All done...
                    return (swordencryptionprofile);
                }

                /// <summary>
                /// Build the task reply...
                /// </summary>
                /// <param name="a_blFirstAction">true for the first action</param>
                /// <returns>true on success</returns>
                public bool BuildTaskReply(bool a_blFirstAction)
                {
                    bool blSuccess;
                    string szAddComma;
                    SwordStream swordstream;
                    SwordTaskResponse swordtaskresponse = m_processswordtask.GetSwordTaskResponse();

                    // Only report on success or successignore...
                    if (   (m_swordstatus != SwordStatus.Success)
                        && (m_swordstatus != SwordStatus.SuccessIgnore)
                        && (m_swordstatus != SwordStatus.NextAction))
                    {
                        swordtaskresponse.AppendTaskResponse
                        (
                            (a_blFirstAction ? "" : ",") +
                            m_swordtaskresponse.GetTaskResponse()
                        );
                        return (true);
                    }

                    // Handle successignore and nextaction...
                    if (    (m_swordstatus == SwordStatus.SuccessIgnore)
                        ||  (m_swordstatus == SwordStatus.NextAction))
                    {
                        // Add this action's response to the one for the task...
                        swordtaskresponse.AppendTaskResponse
                        (
                            (a_blFirstAction ? "" : ",") +
                            m_swordtaskresponse.GetTaskResponse()
                        );
                        return (true);
                    }

                    // Start of the action...
                    m_swordtaskresponse.JSON_OBJ_BGN(2, "");

                    // The vendor (if any) and the action...
                    if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(3, "vendor", ",", m_szVendor);
                    m_swordtaskresponse.JSON_STR_SET(3, "action", ",", m_szAction);

                    // The response...
                    m_swordtaskresponse.JSON_OBJ_BGN(3, "results");
                    m_swordtaskresponse.JSON_TOK_SET(4, "success", "", "true");
                    m_swordtaskresponse.JSON_OBJ_END(3, ",");
                    szAddComma = "";

                    // Only do this bit for the configure action...
                    if (m_szAction == "configure")
                    {
                        // Start of the streams array...
                        m_swordtaskresponse.JSON_ARR_BGN(3, "streams");

                        // List our streams...
                        for (swordstream = GetFirstStream();
                             swordstream != null;
                             swordstream = swordstream.GetNextStream())
                        {
                            // List a stream...
                            blSuccess = swordstream.BuildTaskReply();
                            if (!blSuccess)
                            {
                                break;
                            }
                        }

                        // End of the streams array...
                        m_swordtaskresponse.JSON_ARR_END(3, "");

                        // We need a command...
                        szAddComma = ",";
                    }

                    // Only do this bit for the encryptionProfiles...
                    else if (m_szAction == "encryptionProfiles")
                    {
                        // No action needed at this time...
                    }

                    // Only do this bit for the encryptionReport...
                    else if (m_szAction == "encryptionReport")
                    {
                        // I don't think we get into here, but it's important
                        // to account for all the commands...
                    }

                    // Handle anything we don't recognize, note that we already
                    // have the trailing comma from the results object, so we
                    // don't need to add another...
                    else
                    {
                        // No action needed at this time...
                    }

                    // End of the action...
                    m_swordtaskresponse.JSON_OBJ_END(2, szAddComma);

                    // Add this action's response to the one for the task...
                    swordtaskresponse.AppendTaskResponse
                    (
                        (a_blFirstAction ? "" : ",") +
                        m_swordtaskresponse.GetTaskResponse()
                    );

                    // All done...
                    return (true);
                }

                /// <summary>
                /// Get action for this action...
                /// </summary>
                /// <returns>the action</returns>
                public string GetAction()
                {
                    return (m_szAction);
                }

                /// <summary>
                /// Get the exception for this action...
                /// </summary>
                /// <returns>the exception</returns>
                public string GetException()
                {
                    return (m_szException);
                }

                /// <summary>
                /// Get the first stream for this action...
                /// </summary>
                /// <returns>the head of the streams or null</returns>
                public SwordStream GetFirstStream()
                {
                    return (m_swordstream);
                }

                /// <summary>
                /// Get the first encryptionProfile for this action...
                /// </summary>
                /// <returns>the head of the encryptionProfiles or null</returns>
                public SwordEncryptionProfile GetFirstEncryptionProfile()
                {
                    return (m_swordencryptionprofile);
                }

                /// <summary>
                /// Get the JSON key for this action...
                /// </summary>
                /// <returns>the json key for this action</returns>
                public string GetJsonKey()
                {
                    return (m_szJsonKey);
                }

                /// <summary>
                /// Get the next action...
                /// </summary>
                /// <returns>the next action or null</returns>
                public SwordAction GetNextAction()
                {
                    return (m_swordactionNext);
                }

                /// <summary>
                /// Get the process sword task object that's at
                /// the root of all this fun...
                /// </summary>
                /// <returns>the object which has yummy data in it</returns>
                public ProcessSwordTask GetProcessSwordTask()
                {
                    return (m_processswordtask);
                }

                /// <summary>
                /// Get the status for this action...
                /// </summary>
                /// <returns>the status</returns>
                public SwordStatus GetSwordStatus()
                {
                    return (m_swordstatus);
                }

                /// <summary>
                /// Get the sword task response object...
                /// </summary>
                /// <returns>the object</returns>
                public SwordTaskResponse GetSwordTaskResponse()
                {
                    return (m_swordtaskresponse);
                }

                /// <summary>
                /// Get the vendor for this action...
                /// </summary>
                /// <returns>the vendor</returns>
                public string GetVendor()
                {
                    return (m_szVendor);
                }

                /// <summary>
                /// Process this action, this is where we examine what
                /// we've deserialized, we're not running it yet...
                /// </summary>
                /// <returns>the status of the processing</returns>
                public SwordStatus Process()
                {
                    SwordStatus swordstatus;
                    SwordStream swordstream;
                    SwordEncryptionProfile swordencryptionprofile;

                    // Assume success...
                    m_swordstatus = SwordStatus.Run;

                    // Switch
                    switch (GetAction())
                    {
                        // We have no idea what this is...
                        default:
                            // Apply our exceptions...
                            switch (m_szException)
                            {
                                default:
                                case "ignore":
                                    // Keep going...
                                    m_swordstatus = SwordStatus.SuccessIgnore;
                                    break;
                                case "nextAction":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextAction;
                                    m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".action", "invalidValue", -1);
                                    return (m_swordstatus);
                                case "nextEncryptionProfile":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextEncryptionProfile;
                                    return (m_swordstatus);
                                case "nextStream":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextStream;
                                    return (m_swordstatus);
                                case "fail":
                                    // Whoops, time to empty the pool...
                                    m_swordstatus = SwordStatus.Fail;
                                    m_swordtaskresponse.SetError("fail", m_szJsonKey + ".action", "invalidValue", -1);
                                    return (m_swordstatus);
                            }
                            break;

                        // Handle the configure action...
                        case "configure":
                            // Invoke the process function for each of our streams...
                            for (swordstream = m_swordstream;
                                 swordstream != null;
                                 swordstream = swordstream.GetNextStream())
                            {
                                // Process this stream (and all of its contents)...
                                swordstatus = swordstream.Process();

                                // We've been asked to go to the next stream...
                                if (swordstatus == SwordStatus.NextStream)
                                {
                                    continue;
                                }

                                // Check the result...
                                if (    (swordstatus != SwordStatus.Run)
                                    &&  (swordstatus != SwordStatus.Success)
                                    &&  (swordstatus != SwordStatus.SuccessIgnore))
                                {
                                    m_swordstatus = swordstatus;
                                    return (m_swordstatus);
                                }
                            }
                            break;

                        // Handle the encryptionProfiles action...
                        case "encryptionProfiles":
                            // Invoke the process function for each of our encryptionProfiles...
                            for (swordencryptionprofile = m_swordencryptionprofile;
                                 swordencryptionprofile != null;
                                 swordencryptionprofile = swordencryptionprofile.GetNextEncryptionProfile())
                            {
                                // Process this encryptionProfile (and all of its contents)...
                                swordstatus = swordencryptionprofile.Process();

                                // We've been asked to go to the next encryptionProfile...
                                if (swordstatus == SwordStatus.NextEncryptionProfile)
                                {
                                    continue;
                                }

                                // Check the result...
                                if (    (swordstatus != SwordStatus.Run)
                                    &&  (swordstatus != SwordStatus.Success)
                                    &&  (swordstatus != SwordStatus.SuccessIgnore))
                                {
                                    m_swordstatus = swordstatus;
                                    return (m_swordstatus);
                                }
                            }
                            break;

                        // Handle the encryptionReport action...
                        case "encryptionReport":
                            // report back what we have, if anything...
                            break;
                    }

                    // All done...
                    return (m_swordstatus);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                /// <summary>
                /// Set the exception for this action...
                /// </summary>
                /// <param name="a_szException"></param>
                public void SetException(string a_szException)
                {
                    m_szException = a_szException;
                }

                /// <summary>
                /// Set the status for this action...
                /// </summary>
                /// <param name="a_eswordstatus"></param>
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
                    m_swordstatus = a_swordstatus;
                }

                /// <summary>
                /// Next one in the list, note if we're the head...
                /// </summary>
                private SwordAction m_swordactionNext;

                /// <summary>
                /// Our main object...
                /// </summary>
                private ProcessSwordTask m_processswordtask;

                /// <summary>
                /// Our response...
                /// </summary>
                private SwordTaskResponse m_swordtaskresponse;

                /// <summary>
                /// Who owns us?
                /// </summary>
                private VendorOwner m_vendorowner;

                /// <summary>
                /// The status of the item...
                /// </summary>
                private SwordStatus m_swordstatus;

                /// <summary>
                /// The index of this item in the JSON string...
                /// </summary>
                private string m_szJsonKey;

                /// <summary>
                /// The exception for this action...
                /// </summary>
                private string m_szException;

                /// <summary>
                /// Vendor UUID...
                /// </summary>
                private string m_szVendor;

                /// <summary>
                /// The command identifier...
                /// </summary>
                private string m_szAction;

                /// <summary>
                /// The image streams...
                /// </summary>
                private SwordStream m_swordstream;

                /// <summary>
                /// The encryption profiles...
                /// </summary>
                private SwordEncryptionProfile m_swordencryptionprofile;
            }

            /// <summary>
            /// Each stream contains a list of sources.  All of the sources are used to
            /// capture image data.  This can result in some odd but perfectly acceptable
            /// combinations: such as a feeder and a flatbed, in which case the session
            /// would capture all of the data from the feeder, then an image from the
            /// flatbed.
            /// 
            /// The more typical example would be a feeder or a flatbed, or separate
            /// settings for the front and rear of a feeder.
            /// </summary>
            sealed class SwordStream
            {
                /// <summary>
                /// Constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">object for the response</param>
                /// <param name="a_swordstreamHead">head of the streams or null</param>
                /// <param name="a_szJsonKey">actions[].streams[]</param>
                /// <param name="a_szStream">name of the stream</param>
                /// <param name="a_szException">exception for this stream</param>
                /// <param name="a_szVendor">vendor id, if any</param>
                public SwordStream
                (
                    ProcessSwordTask a_processsowrdtask,
	                SwordTaskResponse a_swordtaskresponse,
                    SwordStream a_swordstreamHead,
	                string a_szJsonKey,
                    string a_szStreamName,
                    string a_szException,
                    string a_szVendor
                )
                {
	                // If the vendor isn't us, then skip it, this isn't subject to exceptions,
	                // so we return right away...
	                m_vendorowner = a_processsowrdtask.GetVendorOwner(a_szVendor);
	                if (m_vendorowner == VendorOwner.Unknown)
	                {
		                m_swordstatus = SwordStatus.VendorMismatch;
		                return;
	                }

                    // Init stuff...
                    m_processswordtask = a_processsowrdtask;
                    m_swordtaskresponse = a_swordtaskresponse;
	                m_swordstatus = SwordStatus.Success;
                    m_szJsonKey = a_szJsonKey;
                    m_szStreamName = a_szStreamName;
                    m_szException = a_szException;
                    m_szVendor = a_szVendor;

                    // If we didn't get an exception, or if the exception is
                    // the nextaction placeholder, then assign the nextstream
                    // placeholder...
                    if (string.IsNullOrEmpty(m_szException) || (m_szException == "@nextActionOrIgnore"))
                    {
                        m_szException = "@nextStreamOrIgnore";
                    }

	                // We're the head of the list...
	                if (a_swordstreamHead == null)
	                {
                        // nothing needed...
                    }

                    // We're being appended to the list...
                    else
                    {
		                SwordStream swordstreamParent;
		                for (swordstreamParent = a_swordstreamHead; swordstreamParent.m_swordstreamNext != null; swordstreamParent = swordstreamParent.m_swordstreamNext) ;
		                swordstreamParent.m_swordstreamNext = this;
                    }
                }

                /// <summary>
                /// Add a source to the stream...
                /// </summary>
                /// <param name="a_szJsonKey">actions[].streams[].source</param>
                /// <param name="a_szSourceName">name for this source</param>
                /// <param name="a_szSource">source we're adding</param>
                /// <param name="a_szException">exception for this source</param>
                /// <param name="a_szVendor">vendor id, if any</param>
                /// <returns></returns>
                public SwordSource AppendSource
                (
	                string a_szJsonKey,
                    string a_szSourceName,
                    string a_szSource,
                    string a_szException,
                    string a_szVendor
                )
                {
                    SwordSource swordsource;

                    // Allocate and init our beastie...
                    swordsource = new SwordSource(m_processswordtask, m_swordtaskresponse, m_swordsource, a_szJsonKey, a_szSourceName, a_szSource, a_szException, a_szVendor);
                    if (swordsource == null)
                    {
                        return (null);
                    }

                    // We're not supported...
                    if (swordsource.GetSwordStatus() == SwordStatus.VendorMismatch)
                    {
                        swordsource = null;
                        return (null);
                    }

                    // Make us the head of the list...
                    if (m_swordsource == null)
                    {
                        m_swordsource = swordsource;
                    }

                    // All done...
                    return (swordsource);
                }

                /// <summary>
                /// Build the task reply...
                /// </summary>
                /// <returns>true on success</returns>
                public bool BuildTaskReply()
                {
                    bool blSuccess;
                    SwordSource swordsource;

                    // Only report on success...
                    if (m_swordstatus != SwordStatus.Success)
                    {
                        return (true);
                    }

                    // Start of the stream...
                    m_swordtaskresponse.JSON_OBJ_BGN(4, "");

                    // The vendor (if any) and the stream's name...
                    if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(5, "vendor", ",", m_szVendor);
                    m_swordtaskresponse.JSON_STR_SET(5, "name", ",", m_szStreamName);

                    // Start of the sources array...
                    m_swordtaskresponse.JSON_ARR_BGN(5, "sources");

                    // List our sources...
                    for (swordsource = GetFirstSource();
                         swordsource != null;
                         swordsource = swordsource.GetNextSource())
                    {
                        // List a stream...
                        blSuccess = swordsource.BuildTaskReply();
                        if (!blSuccess)
                        {
                            break;
                        }
                    }

                    // End of the sources array...
                    m_swordtaskresponse.JSON_ARR_END(5, "");

                    // End of the stream...
                    m_swordtaskresponse.JSON_OBJ_END(4, ",");

                    // All done...
                    return (true);
                }

                /// <summary>
                /// Get the name of the stream...
                /// </summary>
                /// <returns>the name</returns>
                public string GetName()
                {
                    return (m_szStreamName);
                }

                /// <summary>
                /// Get the next stream in the list...
                /// </summary>
                /// <returns>the next stream or null</returns>
                public SwordStream GetNextStream()
                {
                    return (m_swordstreamNext);
                }

                /// <summary>
                /// Get the first source in the stream...
                /// </summary>
                /// <returns>the first source or null</returns>
                public SwordSource GetFirstSource()
                {
                    return (m_swordsource);
                }

                /// <summary>
                /// Get the exception for this stream...
                /// </summary>
                /// <returns>th exception</returns>
                public string GetException()
                {
                    return (m_szException);
                }

                /// <summary>
                /// Get the status for this stream...
                /// </summary>
                /// <returns></returns>
                public SwordStatus GetSwordStatus()
                {
                    return (m_swordstatus);
                }

                /// <summary>
                /// Get the vendor for this stream...
                /// </summary>
                /// <returns></returns>
                public string GetVendor()
                {
                    return (m_szVendor);
                }

                /// <summary>
                /// Process this stream...
                /// </summary>
                /// <returns>the status of processing</returns>
                public SwordStatus Process()
                {
                    SwordStatus swordstatus;
                    SwordSource swordsource;

                    // Assume success...
                    m_swordstatus = SwordStatus.Run;

                    // Make sure we have a name...
                    if (string.IsNullOrEmpty(m_szStreamName))
                    {
                        int szIndex;
                        szIndex = m_szJsonKey.LastIndexOf("[");
                        if (szIndex != -1)
                        {
                            m_szStreamName = "stream" + m_szJsonKey.Substring(szIndex + 1);
                            szIndex = m_szStreamName.LastIndexOf("]");
                            if (szIndex != -1)
                            {
                                m_szStreamName = m_szStreamName.Remove(szIndex);
                            }
                        }
                    }

                    // Invoke the process function for each of our sources...
                    for (swordsource = m_swordsource;
                         swordsource != null;
                         swordsource = swordsource.GetNextSource())
                    {
                        // Process this source (and all of its contents)...
                        swordstatus = swordsource.Process();

                        // Check the result...
                        if (    (swordstatus != SwordStatus.Run)
                            &&  (swordstatus != SwordStatus.Success)
                            &&  (swordstatus != SwordStatus.SuccessIgnore))
                        {
                            m_swordstatus = swordstatus;
                            return (m_swordstatus);
                        }
                    }

                    // All done...
                    return (m_swordstatus);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                /// <summary>
                /// Set the exception for this stream...
                /// </summary>
                /// <param name="a_szException"></param>
                public void SetException(string a_szException)
                {
                    m_szException = a_szException;
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Set the status for this stream...
                ////////////////////////////////////////////////////////////////////////////////
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
                    m_swordstatus = a_swordstatus;
                }

                /// <summary>
                /// Next one in the list, not if we're the head...
                /// </summary>
                private SwordStream m_swordstreamNext;

                /// <summary>
                /// Our main object...
                /// </summary>
                private ProcessSwordTask m_processswordtask;

                /// <summary>
                /// Our response...
                /// </summary>
                private SwordTaskResponse m_swordtaskresponse;

                /// <summary>
                /// Who owns us?
                /// </summary>
                private VendorOwner m_vendorowner;

                /// <summary>
                /// The status of the item...
                /// </summary>
                private SwordStatus m_swordstatus;

                /// <summary>
                /// The name of the stream...
                /// </summary>
                private string m_szStreamName;

                /// <summary>
                /// The index of this item in the JSON string...
                /// </summary>
                private string m_szJsonKey;

                /// <summary>
                /// The default exception for this stream...
                /// </summary>
                private string m_szException;

                /// <summary>
                /// Vendor UUID...
                /// </summary>
                private string m_szVendor;

                /// <summary>
                /// The image sources...
                /// </summary>
                private SwordSource m_swordsource;
            }

            /// <summary>
            /// Each source corresponds to a physical element that captures image data,
            /// like a front or rear camera on a feeder, or a flatbed.  If multiple
            /// sources are included then multistream is being requested.
            /// 
            /// The use of the "any" source is a shorthand for a stream that asks for
            /// images from every source the scanner has to offer.  It should only be
            /// used in the simplest cases or as a exception if other sources have failed.
            /// </summary>
            sealed class SwordSource
            {
                /// <summary>
                /// Constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">our response</param>
                /// <param name="a_swordsourceHead">the first source</param>
                /// <param name="a_szJsonKey">actions[].streams[].sources[]</param>
                /// <param name="a_szSource">name of the source</param>
                /// <param name="a_szException">exception for the source</param>
                /// <param name="a_szVendor">vendor id, if any</param>
                public SwordSource
                (
                    ProcessSwordTask a_processswordtask,
                    SwordTaskResponse a_swordtaskresponse,
                    SwordSource a_swordsourceHead,
	                string a_szJsonKey,
                    string a_szSourceName,
                    string a_szSource,
                    string a_szException,
                    string a_szVendor
                )
                {
	                // If the vendor isn't us, then skip it, this isn't subject to exceptions,
	                // so we return right away...
	                m_vendorowner = a_processswordtask.GetVendorOwner(a_szVendor);
	                if (m_vendorowner == VendorOwner.Unknown)
	                {
		                m_swordstatus = SwordStatus.VendorMismatch;
		                return;
	                }

                    // Init stuff...
                    m_processswordtask = a_processswordtask;
                    m_swordtaskresponse = a_swordtaskresponse;
	                m_swordstatus = SwordStatus.Success;
                    m_szJsonKey = a_szJsonKey;
                    m_szException = a_szException;
                    m_szVendor = a_szVendor;
                    m_szSourceName = a_szSourceName;
                    m_szSource = a_szSource;
                    m_szAutomaticSenseMedium = "";
                    m_szCameraSide = "";
                    m_szDuplexEnabled = "";
                    m_szFeederEnabled = "";

                    // We're the head of the list...
                    if (a_swordsourceHead == null)
	                {
                        // nothing needed...
                    }

                    // We're being appended to the list...
                    else
                    {
		                SwordSource swordsourceParent;
		                for (swordsourceParent = a_swordsourceHead; swordsourceParent.m_swordsourceNext != null; swordsourceParent = swordsourceParent.m_swordsourceNext) ;
		                swordsourceParent.m_swordsourceNext = this;

                    }
                }

                /// <summary>
                /// Add a pixelformat to this source...
                /// </summary>
                /// <param name="a_szJsonKey"></param>
                /// <param name="a_szPixelFormatName"></param>
                /// <param name="a_szPixelFormat"></param>
                /// <param name="a_szException"></param>
                /// <param name="a_szVendor"></param>
                /// <returns>the new pixelFormat</returns>
                public SwordPixelFormat AppendPixelFormat
                (
	                string a_szJsonKey,
                    string a_szPixelFormatName,
                    string a_szPixelFormat,
                    string a_szException,
                    string a_szVendor
                )
                {
                    SwordPixelFormat swordpixelformat;

                    // Allocate and init it...
                    swordpixelformat = new SwordPixelFormat(m_processswordtask, m_swordtaskresponse, m_swordpixelformat, a_szJsonKey, a_szPixelFormatName, a_szPixelFormat, a_szException, a_szVendor);
                    if (swordpixelformat == null)
                    {
                        return (null);
                    }

                    // This is unsupported...
                    if (swordpixelformat.GetSwordStatus() == SwordStatus.VendorMismatch)
                    {
                        swordpixelformat = null;
                        return (null);
                    }

                    // Make us the head of the list...
                    if (m_swordpixelformat == null)
                    {
                        m_swordpixelformat = swordpixelformat;
                    }

                    // All done...
                    return (swordpixelformat);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Build the task reply...
                ////////////////////////////////////////////////////////////////////////////////
                public bool BuildTaskReply()
                {
                    bool blSuccess;
                    SwordPixelFormat swordpixelformat;

                    // Only report on success...
                    if (m_swordstatus != SwordStatus.Success)
                    {
                        return (true);
                    }

                    // Start of the source...
                    m_swordtaskresponse.JSON_OBJ_BGN(6, "");

                    // The vendor (if any) and the source...
                    if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(7, "vendor", ",", m_szVendor);
                    m_swordtaskresponse.JSON_STR_SET(7, "source", ",", m_szSource);
                    m_swordtaskresponse.JSON_STR_SET(7, "name", ",", m_szSourceName);

                    // Start of the pixelFormats array...
                    m_swordtaskresponse.JSON_ARR_BGN(7, "pixelFormats");

                    // List our pixelFormats...
                    for (swordpixelformat = GetFirstPixelFormat();
                         swordpixelformat != null;
                         swordpixelformat = swordpixelformat.GetNextPixelFormat())
                    {
                        // List a pixelFormat...
                        blSuccess = swordpixelformat.BuildTaskReply();
                        if (!blSuccess)
                        {
                            break;
                        }
                    }

                    // End of the pixelFormats array...
                    m_swordtaskresponse.JSON_ARR_END(7, "");

                    // End of the source...
                    m_swordtaskresponse.JSON_OBJ_END(6, ",");

                    // All done...
                    return (true);
                }

                /// <summary>
                /// Return the automatic sense medium setting...
                /// </summary>
                /// <returns>the TWAIN command or an empty string</returns>
                public string GetAutomaticSenseMedium()
                {
                    // We don't support CAP_AUTOMATICSENSEMEDIUM, reporting
                    // an empty string will cause other parts of the code to
                    // skip this capability...
                    if (!m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetAutomaticSenseMedium())
                    {
                        return ("");
                    }

                    // We support it, so return whatever we have...
                    return (m_szAutomaticSenseMedium);
                }

                /// <summary>
                /// Return the camera side setting...
                /// </summary>
                /// <returns>the TWAIN command or an empty string</returns>
                public string GetCameraSide()
                {
                    // We don't support CAP_CAMERASIDES, reporting an empty
                    // string will cause other parts of the code to skip
                    // this capability...
                    if (m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetCameraSides() == "[]")
                    {
                        return ("");
                    }

                    // We support it, so return whatever we have...
                    return (m_szCameraSide);
                }

                /// <summary>
                /// Return the feeder enabled setting...
                /// </summary>
                /// <returns>the TWAIN command or an empty string</returns>
                public string GetFeederEnabled()
                {
                    // We don't support CAP_FEEDERENABLED, reporting an empty
                    // string will cause other parts of the code to skip
                    // this capability, (we need to see both a feeder and a
                    // flatbed to treat this capability as supported)...
                    if (    !m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetFeederDetected()
                        ||  !m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetFlatbedDetected())
                    {
                        return ("");
                    }

                    // We support it, so return whatever we have...
                    return (m_szFeederEnabled);
                }

                /// <summary>
                /// Return the JSON key...
                /// </summary>
                /// <returns>the JSON key</returns>
                public string GetJsonKey()
                {
                    return (m_szJsonKey);
                }

                /// <summary>
                /// Return the name of the source...
                /// </summary>
                /// <returns>the name</returns>
                public string GetName()
                {
                    return (m_szSourceName);
                }

                /// <summary>
                /// Get the next source...
                /// </summary>
                /// <returns>the next source or null</returns>
                public SwordSource GetNextSource()
                {
                    return (m_swordsourceNext);
                }

                /// <summary>
                /// Get the first pixelformat for this source...
                /// </summary>
                /// <returns>the head of the list or null</returns>
                public SwordPixelFormat GetFirstPixelFormat()
                {
                    return (m_swordpixelformat);
                }

                /// <summary>
                /// Get the exception for this source...
                /// </summary>
                /// <returns>the exception</returns>
                public string GetException()
                {
                    return (m_szException);
                }

                /// <summary>
                /// Get the source for this source...
                /// </summary>
                /// <returns>the source</returns>
                public string GetSource()
                {
                    return (m_szSource);
                }

                /// <summary>
                /// Get the status for this source...
                /// </summary>
                /// <returns></returns>
                public SwordStatus GetSwordStatus()
                {
                    return (m_swordstatus);
                }

                /// <summary>
                /// Get the vendor for this source...
                /// </summary>
                /// <returns>the vendor, if any</returns>
                public string GetVendor()
                {
                    return (m_szVendor);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		We don't know our pixelformat at the source, so we use the default
                //		imageformat as a place holder...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus Process()
                {
                    bool blForceAny = false;
                    string szStatus;
                    string szCapability;
                    SwordStatus swordstatus;
                    SwordPixelFormat swordpixelformat;

                    // *************************************************************************
                    // Note that our addressing variables were all initialized to their default
                    // values when this object was made, which is why you don't see them being
                    // set inside of this function...
                    // *************************************************************************

                    // Let's start by assuming that we'll be okay, and we'll adjust as needed...
                    m_swordstatus = SwordStatus.Run;

                    // Make sure we recognize the source...
                    switch (m_szSource)
                    {
                        // So much for that idea...
                        default:
                            // Apply our exceptions...
                            switch (m_szException)
                            {
                                default:
                                case "ignore":
                                    // Keep going...
                                    break;
                                case "nextAction":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextAction;
                                    m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".source", "invalidValue", -1);
                                    return (m_swordstatus);
                                case "nextStream":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextStream;
                                    return (m_swordstatus);
                                case "fail":
                                    // Whoops, time to empty the pool...
                                    m_swordstatus = SwordStatus.Fail;
                                    m_swordtaskresponse.SetError("fail", m_szJsonKey + ".source", "invalidValue", -1);
                                    return (m_swordstatus);
                            }
                            break;

                        // We're good, keep going...
                        case "any":
                        case "feeder":
                        case "feederFront":
                        case "feederRear":
                        case "flatbed":
                            break;
                    }

                    // Make sure we have a name...
                    if (string.IsNullOrEmpty(m_szSourceName))
                    {
                        int szIndex;
                        szIndex = m_szJsonKey.LastIndexOf("[");
                        if (szIndex != -1)
                        {
                            m_szSourceName = "source" + m_szJsonKey.Substring(szIndex + 1);
                            szIndex = m_szSourceName.LastIndexOf("]");
                            if (szIndex != -1)
                            {
                                m_szSourceName = m_szSourceName.Remove(szIndex);
                            }
                        }
                    }

                    // We're a standard TWAIN Direct property, as described in the
                    // TWAIN Direct Specification...
                    #region TWAIN Direct

                    if (m_vendorowner == VendorOwner.TwainDirect)
                    {
                        // ANY: use the default for this scanner, we also come here if
                        // not given a source value...
                        if (    string.IsNullOrEmpty(m_szSource)
                            ||  (m_szSource == "any"))
                        {
                            blForceAny = true;
                            m_szAutomaticSenseMedium = "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                            m_szFeederEnabled = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                            m_szCameraSide = "CAP_CAMERASIDE,TWON_ONEVALUE,TWTY_UINT16,0"; // TWCS_BOTH
                        }

                        // FEEDER/FEEDERFRONT/FEEDERREAR: but don't lose the default elevator value...
                        else if (   (m_szSource == "feeder")
                                 || (m_szSource == "feederFront")
                                 || (m_szSource == "feederRear"))
                        {
                            // Address the feeder...
                            m_szFeederEnabled = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1";

                            // Feeder front and rear...
                            if (m_szSource == "feeder")
                            {
                                m_szDuplexEnabled = "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                                m_szCameraSide = "CAP_CAMERASIDE,TWON_ONEVALUE,TWTY_UINT16,0"; // TWCS_BOTH
                            }

                            // Feeder front...
                            else if (m_szSource == "feederFront")
                            {
                                m_szFeederEnabled = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                                m_szCameraSide = "CAP_CAMERASIDE,TWON_ONEVALUE,TWTY_UINT16,1"; // TWCS_TOP
                            }

                            // Feeder rear...
                            else if (m_szSource == "feederRear")
                            {
                                m_szFeederEnabled = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                                m_szCameraSide = "CAP_CAMERASIDE,TWON_ONEVALUE,TWTY_UINT16,2"; // TWCS_BOTTOM
                            }
                        }

                        // FLATBED: ask for the flatbed...
                        // TBD, gotta have a way to tell the standard TWAIN Direct
                        // flatbed value from the Vendor specific flatbed, so we
                        // can report it back properly...
                        else if (m_szSource == "flatbed")
                        {
                            m_szFeederEnabled = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,0";
                        }

                        // We don't recognize this item in any way shape or form, so
                        // use the default, but report our failure...
                        else
                        {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            blForceAny = true;
                            m_szAutomaticSenseMedium = "CAP_AUTOMATICSENSEMEDIUM,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                            m_szFeederEnabled = "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                            m_szCameraSide = "CAP_CAMERASIDE,TWON_ONEVALUE,TWTY_UINT16,0"; // TWCS_BOTH
                            goto ABORT;
                        }
                    }

                    #endregion


                    // Negotiate the source...
                    #region Negotiate the source...

                    // Feeder enabled...
                    if (!string.IsNullOrEmpty(m_szSource) && (m_szSource != "any"))
                    {
                        szStatus = "";
                        szCapability = m_szFeederEnabled;
                        m_processswordtask.m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    }

                    // Duplex enabled...
                    if (!string.IsNullOrEmpty(m_szDuplexEnabled))
                    {
                        szStatus = "";
                        szCapability = m_szDuplexEnabled;
                        m_processswordtask.m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szCapability, ref szStatus);
                    }

                    #endregion


                    // Handle problems...
                    #region Handle problems

                    // Decide if we need to bail...
                    ABORT:

                    // Only if not successful...
                    if (m_swordstatus != SwordStatus.Run)
                    {
                        // Apply our exceptions...
                        switch (m_szException)
                        {
                            default:
                                m_swordstatus = SwordStatus.SuccessIgnore;
                                return (m_swordstatus);
                            case "nextAction":
                                m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".source", "invalidValue", -1);
                                m_swordstatus = SwordStatus.NextAction;
                                return (m_swordstatus);
                            case "nextStream":
                                m_swordstatus = SwordStatus.NextStream;
                                return (m_swordstatus);
                            case "fail":
                                m_swordtaskresponse.SetError("fail", m_szJsonKey + ".source", "invalidValue", -1);
                                m_swordstatus = SwordStatus.Fail;
                                return (m_swordstatus);
                        }
                    }

                    // Force the name to "any", we do this down here so
                    // that we don't interfere with the original name if
                    // have to error out, and so that we know we'll have
                    // a meaningful value...
                    if (blForceAny)
                    {
                        m_szSource = "any";
                    }

                    #endregion


                    // Process all of the pixelformats that we detect...
                    #region Process pixelFormats

                    // Invoke the process function for each of our pixelformats...
                    for (swordpixelformat = m_swordpixelformat;
                         swordpixelformat != null;
                         swordpixelformat = swordpixelformat.GetNextPixelFormat())
                    {
                        // Process this pixelformat (and all of its contents)...
                        swordstatus = swordpixelformat.Process(m_szException, m_szAutomaticSenseMedium, m_szCameraSide, m_szDuplexEnabled, m_szFeederEnabled);

                        // Check the result, we only continue on success and successignore,
                        // anything else kicks us out...
                        if (    (swordstatus != SwordStatus.Run)
                            &&  (swordstatus != SwordStatus.Success)
                            &&  (swordstatus != SwordStatus.SuccessIgnore))
                        {
                            m_swordstatus = swordstatus;
                            return (m_swordstatus);
                        }
                    }

                    #endregion


                    // Return with whatever we currently have for a status...
                    return (m_swordstatus);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                /// <summary>
                /// Set the exception for this source...
                /// </summary>
                /// <param name="a_szException"></param>
                public void SetException(string a_szException)
                {
                    m_szException = a_szException;
                }

                /// <summary>
                /// Set the status for this source...
                /// </summary>
                /// <param name="a_eswordstatus"></param>
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
                    m_swordstatus = a_swordstatus;
                }

                /// <summary>
                /// Next one in the list, note if we're the head...
                /// </summary>
                private SwordSource m_swordsourceNext;

                /// <summary>
                /// Our main object...
                /// </summary>
                private ProcessSwordTask m_processswordtask;

                /// <summary>
                /// Our response...
                /// </summary>
                private SwordTaskResponse m_swordtaskresponse;

                /// <summary>
                /// Who owns us?
                /// </summary>
                private VendorOwner m_vendorowner;

                /// <summary>
                /// The status of the item...
                /// </summary>
                private SwordStatus m_swordstatus;

                /// <summary>
                /// The name of the source...
                /// </summary>
                private string m_szSourceName;

                /// <summary>
                /// The index of this item in the JSON string...
                /// </summary>
                private string m_szJsonKey;

                /// <summary>
                /// The default exception for all items in this source...
                /// </summary>
                private string m_szException;

                /// <summary>
                /// Vendor UUID...
                /// </summary>
                private string m_szVendor;

                /// <summary>
                /// Source of images (ex: any, feederduplex, feederfront, flatbed, etc)...
                /// </summary>
                private string m_szSource;

                /// <summary>
                // The format list contains one or more formats.  This may
                // correspond to physical capture elements, but more usually
                // are capture settings on a source.  If multiple formats
                // appear in the same source it's an "OR" operation.  The
                // best fit is selected.  This is how the imageformat can be
                // automatically detected.
                /// </summary>
                private SwordPixelFormat m_swordpixelformat;

                // Our address...
                private string m_szAutomaticSenseMedium;
                private string m_szCameraSide;
                private string m_szDuplexEnabled;
                private string m_szFeederEnabled;
            }

            /// <summary>
            ///	A list of zero or more pixelFormats for a source, more than one signals
            ///	the desire to automatically select the pixelFormat that best represents
            ///	the contents of a side of a sheet of paper (color vs bitonal).  We store
            ///	our database items at this level, along with information about the
            ///	papersource / camera / imageformat...
            /// </summary>
            sealed class SwordPixelFormat
            {

                /// <summary>
                /// Constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">our response</param>
                /// <param name="a_swordpixelformatHead">the first pixelFormat</param>
                /// <param name="a_szJsonKey">actions[].streams[].sources[].pixelFormats[]</param>
                /// <param name="a_szPixelFormat">the pixelFormat</param>
                /// <param name="a_szException">the exception</param>
                /// <param name="a_szVendor">the vendor, if any</param>
                public SwordPixelFormat
                (
                    ProcessSwordTask a_processswordtask,
                    SwordTaskResponse a_swordtaskresponse,
                    SwordPixelFormat a_swordpixelformatHead,
                    string a_szJsonKey,
                    string a_szPixelFormatName,
                    string a_szPixelFormat,
                    string a_szException,
                    string a_szVendor
                )
                {
                    // If the vendor isn't us, then skip it, this isn't subject
                    // to exceptions, so we return here...
                    m_vendorowner = a_processswordtask.GetVendorOwner(a_szVendor);
                    if (m_vendorowner == VendorOwner.Unknown)
                    {
                        m_swordstatus = SwordStatus.VendorMismatch;
                        return;
                    }

                    // Init stuff...
                    m_processswordtask = a_processswordtask;
                    m_swordtaskresponse = a_swordtaskresponse;
                    m_swordstatus = SwordStatus.Success;
                    m_szJsonKey = a_szJsonKey;
                    m_szPixelFormatName = a_szPixelFormatName;
                    m_szPixelFormat = a_szPixelFormat;
                    m_szException = a_szException;
                    m_szVendor = a_szVendor;
                    m_capabilityCompression = null;
                    m_capabilityPixeltype = null;
                    m_capabilityResolution = null;
                    m_capabilityXfercount = null;
                    m_aswordattribute = null;

                    // Our addressing attributes...
                    m_swordattributeAutomaticsensemedium = new SwordAttribute(a_processswordtask, a_swordtaskresponse, null, a_szJsonKey, "pixelFormat", "CAP_AUTOMATICSENSEMEDIUM", a_szException, a_szVendor);
                    m_swordattributeFeederenabled = new SwordAttribute(a_processswordtask, a_swordtaskresponse, null, a_szJsonKey, "pixelFormat", "CAP_FEEDERENABLED", a_szException, a_szVendor);
                    m_swordattributeDuplexenabled = new SwordAttribute(a_processswordtask, a_swordtaskresponse, null, a_szJsonKey, "pixelFormat", "CAP_DUPLEXENABLED", a_szException, a_szVendor);
                    m_swordattributeCameraside = new SwordAttribute(a_processswordtask, a_swordtaskresponse, null, a_szJsonKey, "pixelFormat", "CAP_CAMERASIDE", a_szException, a_szVendor);
                    m_swordattributePixeltype = new SwordAttribute(a_processswordtask, a_swordtaskresponse, null, a_szJsonKey, "pixelFormat", "ICAP_PIXELTYPE", a_szException, a_szVendor);
                    if ((m_swordattributeAutomaticsensemedium == null)
                        || (m_swordattributeFeederenabled == null)
                        || (m_swordattributeDuplexenabled == null)
                        || (m_swordattributeCameraside == null)
                        || (m_swordattributePixeltype == null))
                    {
                        m_swordstatus = SwordStatus.Fail;
                        return;
                    }

                    // We're the head of the list...
                    if (a_swordpixelformatHead == null)
                    {
                        // nothing needed...
                    }

                    // We're being appended to the list...
                    else
                    {
                        SwordPixelFormat swordpixelformatParent;
                        for (swordpixelformatParent = a_swordpixelformatHead; swordpixelformatParent.m_swordpixelformatNext != null; swordpixelformatParent = swordpixelformatParent.m_swordpixelformatNext) ;
                        swordpixelformatParent.m_swordpixelformatNext = this;

                    }

                    // All done...
                    SetSwordStatus(SwordStatus.Success);
                    return;
                }

                /// <summary>
                /// Add an attribute...
                /// </summary>
                /// <param name="a_szJsonKey"></param>
                /// <param name="a_szAttribute"></param>
                /// <param name="a_szException"></param>
                /// <param name="a_szVendor"></param>
                /// <returns></returns>
                public SwordAttribute AddAttribute
                (
                    string a_szJsonKey,
                    string a_szAttribute,
                    string a_szException,
                    string a_szVendor
                )
                {
                    SwordAttribute swordattribute;

                    // Create an attribute object, note that we don't have a
                    // TWAIN name at this point...
                    swordattribute = new SwordAttribute(m_processswordtask, m_swordtaskresponse, m_swordattribute, a_szJsonKey, a_szAttribute, "", a_szException, a_szVendor);
                    if (swordattribute == null)
                    {
                        return (null);
                    }

                    // We're not supported...
                    if (swordattribute.GetSwordStatus() == SwordStatus.VendorMismatch)
                    {
                        swordattribute = null;
                        return (null);
                    }

                    // If this is the first time, store it at the head of
                    // the list.  Note that we keep the list so that we
                    // have a record of all of the attributes we encounter,
                    // while the array only has the attributes that we will
                    // send to the scanner...
                    if (m_swordattribute == null)
                    {
                        m_swordattribute = swordattribute;
                    }

                    // Any status other than success stops us here...
                    if (swordattribute.GetSwordStatus() != SwordStatus.Success)
                    {
                        return (swordattribute);
                    }

                    // All done...
                    return (swordattribute);
                }

                /// <summary>
                /// Build the task reply...
                /// </summary>
                /// <returns></returns>
                public bool BuildTaskReply()
                {
                    bool blSuccess;
                    SwordAttribute swordattribute;

                    // Only report on success...
                    if (m_swordstatus != SwordStatus.Success)
                    {
                        return (true);
                    }

                    // Start of the pixelFormat...
                    m_swordtaskresponse.JSON_OBJ_BGN(8, "");

                    // The vendor (if any) and the pixelFormat...
                    if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(9, "vendor", ",", m_szVendor);
                    m_swordtaskresponse.JSON_STR_SET(9, "pixelFormat", ",", m_szPixelFormat);
                    m_swordtaskresponse.JSON_STR_SET(9, "name", ",", m_szPixelFormatName);

                    // Start of the attributes array...
                    m_swordtaskresponse.JSON_ARR_BGN(9, "attributes");

                    // List our attributes...
                    for (swordattribute = GetFirstAttribute();
                         swordattribute != null;
                         swordattribute = swordattribute.GetNextAttribute())
                    {
                        // List an attribute...
                        blSuccess = swordattribute.BuildTaskReply();
                        if (!blSuccess)
                        {
                            break;
                        }
                    }

                    // End of the attributes array...
                    m_swordtaskresponse.JSON_ARR_END(9, "");

                    // End of the source...
                    m_swordtaskresponse.JSON_OBJ_END(8, ",");

                    // All done...
                    return (true);
                }

                /// <summary>
                /// Get the name for this pixelFormat...
                /// </summary>
                /// <returns>the name</returns>
                public string GetName()
                {
                    return (m_szPixelFormatName);
                }

                /// <summary>
                /// Get the next pixelformat for this source...
                /// </summary>
                /// <returns></returns>
                public SwordPixelFormat GetNextPixelFormat()
                {
                    return (m_swordpixelformatNext);
                }

                /// <summary>
                /// Get the first attribute for this pixelformat...
                /// </summary>
                /// <returns></returns>
                public SwordAttribute GetFirstAttribute()
                {
                    return (m_swordattribute);
                }

                /// <summary>
                /// Get the attribute object...
                /// </summary>
                /// <param name="a_edbid"></param>
                /// <param name=""></param>
                /// <param name="a_blForceArray"></param>
                /// <returns></returns>
                public SwordAttribute GetAttribute
                (
                    string a_szCapability,
                    bool a_blForceArray
                )
                {
                    long ii;
                    long cc;

                    // Look for special items (if allowed)...
                    if (!a_blForceArray)
                    {
                        switch (a_szCapability)
                        {
                            default: break;
                            case "CAP_AUTOMATICSENSEMEDIUM": return (m_swordattributeAutomaticsensemedium);
                            case "CAP_FEEDERENABLED": return (m_swordattributeFeederenabled);
                            case "CAP_DUPLEXENABLED": return (m_swordattributeDuplexenabled);
                            case "CAP_CAMERASIDE": return (m_swordattributeCameraside);
                            case "ICAP_PIXELTYPE": return (m_swordattributePixeltype);
                        }
                    }

                    // We don't have a value...
                    if ((m_aswordattribute == null) || (m_aswordattribute.Length == 0))
                    {
                        return (null);
                    }

                    // Return it from the array, bearing in mind that each capability can
                    // try to set more than one thing...
                    for (ii = 0; ii < m_aswordattribute.Length; ii++)
                    {
                        string[] aszCapability = m_aswordattribute[ii].GetCapability();
                        if ((aszCapability != null) && (aszCapability.Length > 0))
                        {
                            for (cc = 0; cc < aszCapability.Length; cc++)
                            {
                                if (aszCapability[ii].StartsWith(a_szCapability + ","))
                                {
                                    return (m_aswordattribute[ii]);
                                }
                            }
                        }
                    }

                    // No joy...
                    return (null);
                }

                /// <summary>
                /// TWAIN: get the compression...
                /// </summary>
                /// <returns>the object</returns>
                public Capability GetCapabilityCompression()
                {
                    return (m_capabilityCompression);
                }

                /// <summary>
                /// TWAIN: get the pixeltype...
                /// </summary>
                /// <returns>the object</returns>
                public Capability GetCapabilityPixeltype()
                {
                    return (m_capabilityPixeltype);
                }

                /// <summary>
                /// TWAIN: get the resolution...
                /// </summary>
                /// <returns>the object</returns>
                public Capability GetCapabilityResolution()
                {
                    return (m_capabilityResolution);
                }

                /// <summary>
                /// TWAIN: get the xfercount...
                /// </summary>
                /// <returns>the object</returns>
                public Capability GetCapabilityXfercount()
                {
                    return (m_capabilityXfercount);
                }

                /// <summary>
                /// Get the exception for this pixelformat...
                /// </summary>
                /// <returns>the exception</returns>
                public string GetException()
                {
                    return (m_szException);
                }

                /// <summary>
                /// Get the pixelformat value for this pixelformat...
                /// </summary>
                /// <returns>the pixelFormat</returns>
                public string GetPixelFormat()
                {
                    return (m_szPixelFormat);
                }

                /// <summary>
                ///	Process this pixelformat.  We sort out what the topology
                ///	is, and then process all of the attributes...
                /// </summary>
                /// <param name="a_szSourceException"></param>
                /// <param name="a_szAutomaticSenseMedium"></param>
                /// <param name="a_szCameraSide"></param>
                /// <param name="a_szDuplexEnabled"></param>
                /// <param name="a_szFeederEnabled"></param>
                /// <param name="a_szPixelType"></param>
                /// <returns></returns>
                public SwordStatus Process
                (
	                string a_szSourceException,
                    string a_szAutomaticSenseMedium,
                    string a_szCameraSide,
                    string a_szDuplexEnabled,
                    string a_szFeederEnabled
                )
                {
                    SwordStatus swordstatus;
                    SwordAttribute swordattribute;

                    // Assume success, unless told otherwise...
                    m_swordstatus = SwordStatus.Run;

                    // Make sure we have a name...
                    if (string.IsNullOrEmpty(m_szPixelFormatName))
                    {
                        int szIndex;
                        szIndex = m_szJsonKey.LastIndexOf("[");
                        if (szIndex != -1)
                        {
                            m_szPixelFormatName = "pixelFormat" + m_szJsonKey.Substring(szIndex + 1);
                            szIndex = m_szPixelFormatName.LastIndexOf("]");
                            if (szIndex != -1)
                            {
                                m_szPixelFormatName = m_szPixelFormatName.Remove(szIndex);
                            }
                        }
                    }

                    // Make sure we recognize the pixelFormat...
                    switch (m_szPixelFormat)
                    {
                        // So much for that idea...
                        default:
                            // Apply our exceptions...
                            switch (m_szException)
                            {
                                default:
                                case "ignore":
                                    // Keep going...
                                    break;
                                case "nextAction":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextAction;
                                    m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".pixelFormat", "invalidValue", -1);
                                    return (m_swordstatus);
                                case "nextStream":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextStream;
                                    return (m_swordstatus);
                                case "fail":
                                    // Whoops, time to empty the pool...
                                    m_swordstatus = SwordStatus.Fail;
                                    m_swordtaskresponse.SetError("fail", m_szJsonKey + ".pixelFormat", "invalidValue", -1);
                                    return (m_swordstatus);
                            }
                            break;

                        // We're good, keep going...
                        case "any":
                            m_swordattributePixeltype.AppendValue(m_szJsonKey, "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,*", a_szSourceException, m_szVendor);
                            break;

                        case "bw1":
                            m_swordattributePixeltype.AppendValue(m_szJsonKey, "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,0", a_szSourceException, m_szVendor);
                            break;

                        case "gray8":
                            m_swordattributePixeltype.AppendValue(m_szJsonKey, "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,1", a_szSourceException, m_szVendor);
                            break;

                        case "rgb24":
                            m_swordattributePixeltype.AppendValue(m_szJsonKey, "ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,2", a_szSourceException, m_szVendor);
                            break;
                    }


                    // Take care of our topology issue...
                    #region Topology

                    // CAP_AUTOMATICSENSEMEDIUM...
                    if (string.IsNullOrEmpty(a_szAutomaticSenseMedium))
                    {
                        m_swordattributeAutomaticsensemedium.AppendValue(m_szJsonKey, "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,*", a_szSourceException, m_szVendor);
                    }
                    else
                    {
                        m_swordattributeAutomaticsensemedium.AppendValue(m_szJsonKey, a_szAutomaticSenseMedium, a_szSourceException, m_szVendor);
                    }

                    // CAP_CAMERASIDE...
                    if (string.IsNullOrEmpty(a_szCameraSide) || (m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetCameraSides() == "[]"))
                    {
                        m_swordattributeCameraside.AppendValue(m_szJsonKey, "CAP_CAMERASIDE,TWON_ONEVALUE,TWTY_BOOL,*", a_szSourceException, m_szVendor);
                    }
                    else
                    {
                        m_swordattributeCameraside.AppendValue(m_szJsonKey, a_szCameraSide, a_szSourceException, m_szVendor);
                    }

                    // CAP_DUPLEXENABLED...
                    if (string.IsNullOrEmpty(a_szDuplexEnabled))
                    {
                        m_swordattributeDuplexenabled.AppendValue(m_szJsonKey, "CAP_DUPLEXENABLED,TWON_ONEVALUE,TWTY_BOOL,*", a_szSourceException, m_szVendor);
                    }
                    else
                    {
                        m_swordattributeDuplexenabled.AppendValue(m_szJsonKey, a_szDuplexEnabled, a_szSourceException, m_szVendor);
                    }

                    // CAP_FEEDERENABLED...
                    if (string.IsNullOrEmpty(a_szFeederEnabled))
                    {
                        m_swordattributeFeederenabled.AppendValue(m_szJsonKey, "CAP_FEEDERENABLED,TWON_ONEVALUE,TWTY_BOOL,*", a_szSourceException, m_szVendor);
                    }
                    else
                    {
                        m_swordattributeFeederenabled.AppendValue(m_szJsonKey, a_szFeederEnabled, a_szSourceException, m_szVendor);
                    }

                    #endregion


                    // Handle problems...
                    #region Handle problems

                    // Only if not successful...
                    if (m_swordstatus != SwordStatus.Run)
                    {
                        // Apply our exceptions...
                        switch (m_szException)
                        {
                            default:
                                m_swordstatus = SwordStatus.SuccessIgnore;
                                return (m_swordstatus);
                            case "nextAction":
                                m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".pixelFormat", "invalidValue", -1);
                                m_swordstatus = SwordStatus.NextAction;
                                return (m_swordstatus);
                            case "nextStream":
                                m_swordstatus = SwordStatus.NextStream;
                                return (m_swordstatus);
                            case "fail":
                                m_swordtaskresponse.SetError("fail", m_szJsonKey + ".pixelFormat", "invalidValue", -1);
                                m_swordstatus = SwordStatus.Fail;
                                return (m_swordstatus);
                        }
                    }

                    #endregion


                    // Process attributes...
                    #region Process attributes

                    // Process the addressing attributes...
                    m_swordstatus = m_swordattributeFeederenabled.Process(null);
                    if (    (m_swordstatus != SwordStatus.Run)
                        &&  (m_swordstatus != SwordStatus.Success)
                        &&  (m_swordstatus != SwordStatus.SuccessIgnore))
                    {
                        return (m_swordstatus);
                    }
                    m_swordstatus = m_swordattributeCameraside.Process(null);
                    if (    (m_swordstatus != SwordStatus.Run)
                        &&  (m_swordstatus != SwordStatus.Success)
                        &&  (m_swordstatus != SwordStatus.SuccessIgnore))
                    {
                        return (m_swordstatus);
                    }
                    m_swordstatus = m_swordattributePixeltype.Process(null);
                    if (    (m_swordstatus != SwordStatus.Run)
                        &&  (m_swordstatus != SwordStatus.Success)
                        &&  (m_swordstatus != SwordStatus.SuccessIgnore))
                    {
                        return (m_swordstatus);
                    }

                    // If we don't have a value, then get what we currently have...
                    string szPixeltype;
                    if (m_swordattributePixeltype.GetFirstValue() != null)
                    {
                        szPixeltype = m_swordattributePixeltype.GetFirstValue().GetValue();
                    }
                    else
                    {
                        string szStatus;
                        szStatus = "";
                        szPixeltype = "ICAP_PIXELTYPE";
                        m_processswordtask.m_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_GETCURRENT", ref szPixeltype, ref szStatus);
                    }

                    // Invoke the process function for each of our attributes...
                    for (swordattribute = m_swordattribute;
                         swordattribute != null;
                         swordattribute = swordattribute.GetNextAttribute())
                    {
                        // Process this attribute (and all of its contents)...
                        swordstatus = swordattribute.Process(szPixeltype);

                        // Check the result...
                        if (    (swordstatus != SwordStatus.Run)
                            &&  (swordstatus != SwordStatus.Success)
                            &&  (swordstatus != SwordStatus.SuccessIgnore))
                        {
                            m_swordstatus = swordstatus;
                            return (m_swordstatus);
                        }
                    }

                    #endregion


                    // Return with whatever we currently have for a status...
                    return (m_swordstatus);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                /// <summary>
                /// Set the exception for this pixelformat...
                /// </summary>
                /// <param name="a_szException"></param>
                public void SetException(string a_szException)
                {
                    m_szException = a_szException;
                }

                /// <summary>
                /// Set the exception for this pixelformat...
                /// </summary>
                /// <param name="a_swordstatus"></param>
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
                    m_swordstatus = a_swordstatus;
                }

                /// <summary>
                /// Get the exception for this pixelformat...
                /// </summary>
                /// <returns>the status</returns>
                public SwordStatus GetSwordStatus()
                {
                    return (m_swordstatus);
                }

                /// <summary>
                /// Get the vendor for this pixelformat...
                /// </summary>
                /// <returns>the vendor, if any</returns>
                public string GetVendor()
                {
                    return (m_szVendor);
                }

                /// <summary>
                /// Next one in the list...
                /// </summary>
                private SwordPixelFormat m_swordpixelformatNext;

                /// <summary>
                /// Our main object...
                /// </summary>
                private ProcessSwordTask m_processswordtask;

                /// <summary>
                /// Our response...
                /// </summary>
                private SwordTaskResponse m_swordtaskresponse;

                /// <summary>
                /// The status of the item...
                /// </summary>
                private SwordStatus m_swordstatus;

                /// <summary>
                /// The name of the pixelFormat...
                /// </summary>
                private string m_szPixelFormatName;

                /// <summary>
                /// The index of this item in the JSON string...
                /// </summary>
                private string m_szJsonKey;

                /// <summary>
                /// The default exception for all items in this source...
                /// </summary>
                private string m_szException;

                /// <summary>
                /// Vendor UUID...
                /// </summary>
                private string m_szVendor;

                /// <summary>
                /// Vendor info...
                /// </summary>
                private VendorOwner m_vendorowner;

                /// <summary>
                /// Format of images (ex: bw1, gray8, rgb24, etc)
                /// </summary>
                private string m_szPixelFormat;

                /// <summary>
                /// The source/pixelFormat attributes...
                /// </summary>
                private SwordAttribute m_swordattributeAutomaticsensemedium;
                private SwordAttribute m_swordattributeCameraside;
                private SwordAttribute m_swordattributeDuplexenabled;
                private SwordAttribute m_swordattributeFeederenabled;
                private SwordAttribute m_swordattributePixeltype;

                /// <summary>
                /// TWAIN stuff...
                /// </summary>
                private Capability m_capabilityCompression;
                private Capability m_capabilityPixeltype;
                private Capability m_capabilityResolution;
                private Capability m_capabilityXfercount;

                /// <summary>
                /// The first attribute in the list...
                /// </summary>
                private SwordAttribute m_swordattribute;

                /// <summary>
                /// We'll maintain a list of the attributes at this level
                /// to make it easier to parse them, when it's time to
                /// send stuff to the scanner...
                /// </summary>
                private SwordAttribute[] m_aswordattribute;
            }

            /// <summary>
            ///	A list of zero or more attributes for a pixelFormat, all of which are
            ///	used (or at least an attempt is made to use them)...
            /// </summary>
            sealed class SwordAttribute
            {
                /// <summary>
                /// Constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">our response</param>
                /// <param name="a_swordattributeHead">the first attribute</param>
                /// <param name="a_szJsonKey">actions[].streams[].sources[].pixelTypes[].attributes</param>
                /// <param name="a_szSwordAttribute">the sword attribute name</param>
                /// <param name="a_szTwainAttribute">the twain attribute name</param>
                /// <param name="a_szException">the exception for this attribute</param>
                /// <param name="a_szVendor">the vendor id, if any</param>
                public SwordAttribute
                (
                    ProcessSwordTask a_processswordtask,
                    SwordTaskResponse a_swordtaskresponse,
	                SwordAttribute a_swordattributeHead,
	                string a_szJsonKey,
                    string a_szSwordAttribute,
                    string a_szTwainAttribute,
                    string a_szException,
                    string a_szVendor
                )
                {
	                // If the vendor isn't us, then skip it, this isn't subject
	                // to exceptions, so we return here...
	                m_vendorowner = a_processswordtask.GetVendorOwner(a_szVendor);
	                if (m_vendorowner == VendorOwner.Unknown)
	                {
		                m_swordstatus = SwordStatus.VendorMismatch;
		                return;
	                }

                    // Non-zero stuff...
                    m_processswordtask = a_processswordtask;
                    m_swordtaskresponse = a_swordtaskresponse;
	                m_swordstatus = SwordStatus.Success;
	                m_szJsonKey = a_szJsonKey;
	                m_szException = a_szException;
	                m_szVendor = a_szVendor;
	                m_szSwordAttribute = a_szSwordAttribute;
                    m_szTwainAttribute = a_szTwainAttribute;

                    // We're the head of the list...
                    if (a_swordattributeHead == null)
	                {
                        // nothing needed...
                    }

                    // We're being appended to the list...
                    else
                    {
		                SwordAttribute swordattributeParent;
		                for (swordattributeParent = a_swordattributeHead; swordattributeParent.m_swordattributeNext != null; swordattributeParent = swordattributeParent.m_swordattributeNext) ;
		                swordattributeParent.m_swordattributeNext = this;
	                }

	                // All done...
	                return;
                }

                /// <summary>
                /// Append a value to the attribute...
                /// </summary>
                /// <param name="a_szJsonKey">our key</param>
                /// <param name="a_szValue">the value</param>
                /// <param name="a_szException">our exception</param>
                /// <param name="a_szVendor">our vendor id, if any</param>
                /// <returns></returns>
                public SwordValue AppendValue
                (
	                string a_szJsonKey,
                    string a_szValue,
                    string a_szException,
                    string a_szVendor
                )
                {
	                SwordValue swordvalue;

	                // Allocate and init stuff...
	                swordvalue = new SwordValue(m_processswordtask, m_swordtaskresponse, m_swordvalue,a_szJsonKey,a_szValue,a_szException,a_szVendor);
	                if (swordvalue == null)
	                {
		                return (null);
	                }

	                // We're not supported...
	                if (swordvalue.GetSwordStatus() == SwordStatus.VendorMismatch)
	                {
                        swordvalue = null;

                        return (null);
	                }

	                // Make us the head of the list...
	                if (m_swordvalue == null)
	                {
		                m_swordvalue = swordvalue;
	                }

	                // All done...
	                return (swordvalue);
                }

                /// <summary>
                /// Build the task reply...
                /// </summary>
                /// <returns>true on success</returns>
                public bool BuildTaskReply()
                {
	                bool blSuccess;
	                SwordValue swordvalue;

	                // Only report on success...
	                if (m_swordstatus != SwordStatus.Success)
	                {
		                return (true);
	                }

                    // Start of the attribute...
                    m_swordtaskresponse.JSON_OBJ_BGN(10,"");

	                // The vendor (if any) and the attribute...
	                if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(11,"vendor",",",m_szVendor);
                    m_swordtaskresponse.JSON_STR_SET(11,"attribute",",",m_szSwordAttribute);

                    // Start of the values array...
                    m_swordtaskresponse.JSON_ARR_BGN(11,"values");

	                // List our attributes...
	                for (swordvalue = GetFirstValue();
		                 swordvalue != null;
		                 swordvalue = swordvalue.GetNextValue())
	                {
		                // List an attribute...
		                blSuccess = swordvalue.BuildTaskReply();
		                if (!blSuccess)
		                {
			                break;
		                }
	                }

                    // End of the values array...
                    m_swordtaskresponse.JSON_ARR_END(11,"");

                    // End of the attribute...
                    m_swordtaskresponse.JSON_OBJ_END(10,",");

	                // All done...
	                return (true);
                }

                /// <summary>
                /// Get the sword attribute value for this attribute...
                /// </summary>
                /// <returns>the sword attribute</returns>
                public string GetSwordAttribute()
                {
	                return (m_szSwordAttribute);
                }

                /// <summary>
                /// Get the TWAIN attribute value for this attribute...
                /// </summary>
                /// <returns>the twain attribute</returns>
                public string GetTwainAttribute()
                {
                    return (m_szTwainAttribute);
                }

                /// <summary>
                /// Get the TWAIN capability value for this attribute...
                /// </summary>
                /// <returns>the capability</returns>
                public string[] GetCapability()
                {
                    return (m_swordvalue.GetCapability());
                }

                /// <summary>
                /// Get the next attribute...
                /// </summary>
                /// <returns>the next attribute or null</returns>
                public SwordAttribute GetNextAttribute()
                {
	                return (m_swordattributeNext);
                }

                /// <summary>
                /// Get the first value for this attribute...
                /// </summary>
                /// <returns>the sword value</returns>
                public SwordValue GetFirstValue()
                {
	                return (m_swordvalue);
                }

                /// <summary>
                /// Get the exception for this attribute...
                /// </summary>
                /// <returns>the exception</returns>
                public string GetException()
                {
	                return (m_szException);
                }

                /// <summary>
                /// Get the status for this attribute...
                /// </summary>
                /// <returns>the status</returns>
                public SwordStatus GetSwordStatus()
                {
	                return (m_swordstatus);
                }

                /// <summary>
                /// Get the vendor for this attribute...
                /// </summary>
                /// <returns>the vendor id, if any</returns>
                public string GetVendor()
                {
	                return (m_szVendor);
                }

                /// <summary>
                /// Process this attribute, which means try to map it to
                ///	TWAIN capabilities...
                /// </summary>
                /// <returns>the status of the process</returns>
                public SwordStatus Process(string a_szPixelformat)
                {
	                SwordStatus swordstatus;
	                SwordValue swordvalue;

	                // Assume success...
	                m_swordstatus = SwordStatus.Run;

                    // Make sure we recognize the attribute...
                    switch (m_szSwordAttribute)
                    {
                        // So much for that idea...
                        default:
                            // Apply our exceptions...
                            switch (m_szException)
                            {
                                default:
                                case "ignore":
                                    // Keep going...
                                    break;
                                case "nextAction":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextAction;
                                    m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".attribute", "invalidValue", -1);
                                    return (m_swordstatus);
                                case "nextStream":
                                    // We're out of here...
                                    m_swordstatus = SwordStatus.NextStream;
                                    return (m_swordstatus);
                                case "fail":
                                    // Whoops, time to empty the pool...
                                    m_swordstatus = SwordStatus.Fail;
                                    m_swordtaskresponse.SetError("fail", m_szJsonKey + ".attribute", "invalidValue", -1);
                                    return (m_swordstatus);
                            }
                            break;

                        // We're good, keep going...
                        case "compression":
                        case "doubleFeedDectection":
                        case "numberOfSheets":
                        case "pixelFormat":
                        case "resolution":
                            break;
                    }

	                // Invoke the process function for each of our values...
	                for (swordvalue = m_swordvalue;
		                 swordvalue != null;
		                 swordvalue = swordvalue.GetNextValue())
	                {
		                // Process this value (and all of its contents)...
		                swordstatus = swordvalue.Process(a_szPixelformat, GetSwordAttribute(), GetFirstValue());

		                // Check the result...
		                if (	(swordstatus != SwordStatus.Run)
			                &&	(swordstatus != SwordStatus.Success)
			                &&	(swordstatus != SwordStatus.SuccessIgnore))
		                {
			                m_swordstatus = swordstatus;
			                return (m_swordstatus);
		                }
	                }

	                // Return with whatever we currently have for a status...
	                return (m_swordstatus);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                /// <summary>
                /// Set the vendor for this attribute...
                /// </summary>
                /// <param name="a_szException">the exception to set</param>
                public void SetException(string a_szException)
                {
	                m_szException = a_szException;
                }

                /// <summary>
                /// Set the status for this attribute...
                /// </summary>
                /// <param name="a_swordstatus">the status</param>
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
	                m_swordstatus = a_swordstatus;
                }

			    // Next one in the list, note if we're the first...
			    private SwordAttribute		m_swordattributeNext;

                // Our main object...
                private ProcessSwordTask    m_processswordtask;

			    // Our response...
			    private SwordTaskResponse	m_swordtaskresponse;

			    // Who owns us...
			    private VendorOwner			m_vendorowner;

			    // The status of the item...
			    private SwordStatus		    m_swordstatus;

			    // The index of this item in the JSON string...
			    private string				m_szJsonKey;

			    // The exception for this attribute...
			    private string				m_szException;

			    // Vendor UUID...
			    private string				m_szVendor;

                // The SWORD and TWAIN ids of the attribute...
                private string              m_szSwordAttribute;
                private string				m_szTwainAttribute;

			    // The first value in the list...
			    private SwordValue		    m_swordvalue;
            }

            /// <summary>
            /// A list of zero or more values for an attribute, only one will be used...
            /// </summary>
            sealed class SwordValue
            {

                /// <summary>
                /// Constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">our response</param>
                /// <param name="a_swordvalueHead">the first value</param>
                /// <param name="a_szJsonKey">actions[].streams[].sources[].pixelFormats[].attributes[].values[]</param>
                /// <param name="a_szTdValue">the value</param>
                /// <param name="a_szException">the exception</param>
                /// <param name="a_szVendor">the vendor, if any</param>
                public SwordValue
                (
                    ProcessSwordTask a_processswordtask,
	                SwordTaskResponse a_swordtaskresponse,
	                SwordValue a_swordvalueHead,
	                string a_szJsonKey,
                    string a_szTdValue,
                    string a_szException,
                    string a_szVendor
                )
                {
	                // If the vendor isn't us, then skip it, this isn't subject
	                // to exceptions, so we return here...
	                m_vendorowner = a_processswordtask.GetVendorOwner(a_szVendor);
	                if (m_vendorowner == VendorOwner.Unknown)
	                {
		                m_swordstatus = SwordStatus.VendorMismatch;
		                return;
	                }

                    // Init stuff...
                    m_processswordtask = a_processswordtask;
                    m_swordtaskresponse = a_swordtaskresponse;
	                m_swordstatus = SwordStatus.Success;
	                m_szJsonKey = a_szJsonKey;
	                m_szException = a_szException;
	                m_szVendor = a_szVendor;
	                m_szTdValue = a_szTdValue;

                    // We're the head of the list...
                    if (a_swordvalueHead == null)
	                {
                        // nothing needed...
                    }

                    // We're being appended to the list...
                    else
                    {
		                SwordValue swordvalueParent;
		                for (swordvalueParent = a_swordvalueHead; swordvalueParent.m_swordvalueNext != null; swordvalueParent = swordvalueParent.m_swordvalueNext) ;
		                swordvalueParent.m_swordvalueNext = this;
	                }

	                // All done...
	                return;
                }

                /// <summary>
                /// Build the task reply...
                /// </summary>
                /// <returns>true on success</returns>
                public bool BuildTaskReply()
                {
	                // Only report on success...
	                if (m_swordstatus != SwordStatus.Success)
	                {
		                return (true);
	                }

	                // Start of the value...
	                m_swordtaskresponse.JSON_OBJ_BGN(12,"");

	                // The vendor (if any) and the value...
	                if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(13,"vendor",",",m_szVendor);
	                if (string.IsNullOrEmpty(m_szTdValue))
	                {
                        m_swordtaskresponse.JSON_STR_SET(13,"value","",m_szTdValue);
	                }
	                else if (	(m_szTdValue == "false")
			                 ||	(m_szTdValue == "null")
			                 ||	(m_szTdValue == "true"))
	                {
                        m_swordtaskresponse.JSON_STR_SET(13,"value","",m_szTdValue);
	                }
	                else
	                {
                        int iValue;
		                if (!int.TryParse(m_szTdValue,out iValue))
		                {
                            m_swordtaskresponse.JSON_STR_SET(13,"value","",m_szTdValue);
		                }
		                else
		                {
                            m_swordtaskresponse.JSON_NUM_SET(13,"value","",iValue);
		                }
	                }

                    // End of the value...
                    m_swordtaskresponse.JSON_OBJ_END(12,"");

	                // All done...
	                return (true);
                }

                /// <summary>
                /// Return the capability(s) for this value...
                /// </summary>
                /// <returns>the capability(s)</returns>
                public string[] GetCapability()
                {
                    return (m_aszTwValue);
                }

                /// <summary>
                /// Return the exception for this value...
                /// </summary>
                /// <returns>the exception</returns>
                public string GetException()
                {
	                return (m_szException);
                }

                /// <summary>
                /// Return the json key for this value...
                /// </summary>
                /// <returns>the json key</returns>
                public string GetJsonKey()
                {
                    return (m_szJsonKey);
                }

                /// <summary>
                /// Return the next value in our attribute...
                /// </summary>
                /// <returns>the next value</returns>
                public SwordValue GetNextValue()
                {
	                return (m_swordvalueNext);
                }

                /// <summary>
                /// Get the SWORD status for this value...
                /// </summary>
                /// <returns>the status</returns>
                public SwordStatus GetSwordStatus()
                {
	                return (m_swordstatus);
                }

                /// <summary>
                /// Get the task value...
                /// </summary>
                /// <returns>the value</returns>
                public string GetValue()
                {
	                return (m_szTdValue);
                }

                /// <summary>
                /// Return the vendor id for this value....
                /// </summary>
                /// <returns>the vendor id</returns>
                public string GetVendor()
                {
	                return (m_szVendor);
                }

                /// <summary>
                /// Process this value...
                /// </summary>
                /// <param name="a_szAttribute">the TWAIN Direct attribute name</param>
                /// <returns>our status when done</returns>
                public SwordStatus Process(string a_szPixelformat, string a_szAttribute, SwordValue a_swordvalueHead)
                {
	                // Init stuff...
	                m_aszTwValue = null;

                    // Assume success...
                    m_swordstatus = SwordStatus.Run;

                    // TWAIN Direct...
                    #region TWAIN Direct

                    if (m_vendorowner == VendorOwner.TwainDirect) 
	                {
		                // Handle TWAIN Direct here...
		                switch (a_szAttribute)
		                {
			                default:
                                // Apply our exceptions...
                                switch (m_szException)
                                {
                                    default:
                                    case "ignore":
                                        // Keep going...
                                        break;
                                    case "nextAction":
                                        // We're out of here...
                                        m_swordstatus = SwordStatus.NextAction;
                                        m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".value", "invalidValue", -1);
                                        return (m_swordstatus);
                                    case "nextStream":
                                        // We're out of here...
                                        m_swordstatus = SwordStatus.NextStream;
                                        return (m_swordstatus);
                                    case "fail":
                                        // Whoops, time to empty the pool...
                                        m_swordstatus = SwordStatus.Fail;
                                        m_swordtaskresponse.SetError("fail", m_szJsonKey + ".value", "invalidValue", -1);
                                        return (m_swordstatus);
                                }
                                break;

			                case "automaticDeskew":
				                if (ProcessAutomaticdeskew() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;

                            case "compression":
                                if (ProcessCompression(a_szPixelformat) != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

                            case "continuousScan":
                                if (ProcessContinuousscan() != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

			                case "contrast":
				                if (ProcessContrast() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;

			                case "cropping":
				                if (ProcessCropping() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;

                            case "discardBlankImages":
                                if (ProcessDiscardblankimages() != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

                            case "doubleFeedDetection":
                                if (ProcessDoublefeeddetection() != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

                            case "imageMerge":
                                if (ProcessImagemerge() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;

			                case "noiseFilter":
				                if (ProcessNoisefilter() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;

			                case "numberOfSheets":
				                if (ProcessNumberofsheets() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;

                            case "pixelFormat":
                                // Just accept it...
                                break;

                            case "resolution":
                                if (ProcessResolution(a_swordvalueHead) != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

                            case "rotation":
                                if (ProcessRotation() != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

                            case "sheetHandling":
                                if (ProcessSheethandling() != SwordStatus.Success)
                                {
                                    goto ABORT;
                                }
                                break;

                            case "threshold":
				                if (ProcessThreshold() != SwordStatus.Success)
				                {
					                goto ABORT;
				                }
				                break;
		                }
	                }

	                #endregion


	                // Handle problems...
	                ABORT:

	                // Only if not successful...
	                if (m_swordstatus != SwordStatus.Run)
	                {
                        // Apply our exceptions...
                        switch (m_szException)
                        {
                            default:
                                m_swordstatus = SwordStatus.SuccessIgnore;
                                return (m_swordstatus);
                            case "nextAction":
                                m_swordtaskresponse.SetError("nextAction", m_szJsonKey + ".value", "invalidValue", -1);
                                m_swordstatus = SwordStatus.NextAction;
                                return (m_swordstatus);
                            case "nextStream":
                                m_swordstatus = SwordStatus.NextStream;
                                return (m_swordstatus);
                            case "fail":
                                m_swordtaskresponse.SetError("fail", m_szJsonKey + ".value", "invalidValue", -1);
                                m_swordstatus = SwordStatus.Fail;
                                return (m_swordstatus);
                        }         
	                }

	                // Return with whatever we currently have for a status...
	                return (m_swordstatus);
                }

                /// <summary>
                /// Process automaticdeskew...
                /// </summary>
                /// <returns>our status</returns>
                public SwordStatus ProcessAutomaticdeskew()
                {
	                if (m_szTdValue == "on")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_AUTOMATICDESKEW,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                    }

	                else if (m_szTdValue == "off")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_AUTOMATICDESKEW,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                    }

                    else
	                {
		                m_swordstatus = SwordStatus.SuccessIgnore;
		                return (m_swordstatus);
	                }

	                // All done...
	                return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process compression...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessCompression(string a_szPixelFormat)
                {
                    // Basic scanners are only allowed uncompressed images, because
                    // we're going to use native transfer, which is more likely to
                    // result in a successful scan...
                    if (m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetTwainDirectSupport() == DeviceRegister.TwainDirectSupport.Basic)
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,0"; // TWCP_NONE
                    }

                    // TWAIN Direct from this point down...
                    else if (m_szTdValue == "none")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,0"; // TWCP_NONE
                    }

	                else if (m_szTdValue == "group4")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,5"; // TWCP_GROUP4
                    }

	                else if (m_szTdValue == "jpeg")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,6"; // TWCP_JPEG
                    }

                    else if (m_szTdValue == "autoVersion1")
	                {
                        string szTwPixelType = "*";
                        string[] asz = a_szPixelFormat.Split(','); // ex: ICAP_PIXELTYPE,TWON_ONEVALUE,TWTY_UINT16,2
                        if ((asz != null) || (asz.Length >= 4))
                        {
                            szTwPixelType = asz[3];
                        }
		                switch (szTwPixelType)
		                {
			                default:
                            case "*":
				                m_swordstatus = SwordStatus.SuccessIgnore;
				                return (SwordStatus.SuccessIgnore);
			                case "0": // TWPT_BW
                                m_aszTwValue = new string[1];
                                m_aszTwValue[0] = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,5"; // TWCP_GROUP4
                                break;
			                case "1": // TWPT_GRAY
                            case "2": // TWPT_RGB
                                m_aszTwValue = new string[1];
                                m_aszTwValue[0] = "ICAP_COMPRESSION,TWON_ONEVALUE,TWTY_UINT16,6"; // TWCP_JPEG
                                break;
		                }
	                }

	                // Ruh-roh...
	                else
	                {
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (SwordStatus.SuccessIgnore);
                    }

                    // All done...
                    return (SwordStatus.Success);
                }

                /// <summary>
                /// Process continuousScan...
                /// </summary>
                /// <returns></returns>
                public SwordStatus ProcessContinuousscan()
                {
                    // Fast batch scan...
                    if (m_szTdValue == "on")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_AUTOSCAN,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                        return (m_swordstatus);
                    }

                    // Page on demand, releaseImageBlocks will cause the next sheet
                    // of paper to be read...
                    if (m_szTdValue == "off")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_AUTOSCAN,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        return (m_swordstatus);
                    }

                    // No joy...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process contrast...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessContrast()
                {
                    // Just take the value, TWAIN will validate it...
                    m_aszTwValue = new string[1];
                    m_aszTwValue[0] = "ICAP_CONTRAST,TWON_ONEVALUE,TWTY_FIX32," + m_szTdValue;

                    // All done...
                    return (SwordStatus.Success);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process croppingmode...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessCropping()
                {
	                if (m_szTdValue == "automatic")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_AUTOMATICBORDERDETECTION,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                        return (m_swordstatus);
                    }

	                if (m_szTdValue == "automaticMultiple")
	                {
                        // Not supported...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "fixed")
	                {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_AUTOMATICBORDERDETECTION,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        m_aszTwValue[1] = "ICAP_AUTOMATICLENGTHDETECTION,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "fixedAutomaticLength")
	                {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_AUTOMATICBORDERDETECTION,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        m_aszTwValue[1] = "ICAP_AUTOMATICLENGTHDETECTION,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "long")
	                {
                        // Not supported...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "relative")
	                {
                        // Not supported...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    // Ruh-roh...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                /// <summary>
                /// Process discardBlankImage...
                /// </summary>
                /// <returns>our status</returns>
                public SwordStatus ProcessDiscardblankimages()
                {
                    // Toss blank images...
                    if (m_szTdValue == "on")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_AUTODISCARDBLANKPAGES,TWON_ONEVALUE,TWTY_INT32,-1"; // TWBP_AUTO
                        return (m_swordstatus);
                    }

                    // Keep blank images...
                    if (m_szTdValue == "off")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_AUTODISCARDBLANKPAGES,TWON_ONEVALUE,TWTY_INT32,-2"; // TWBP_DISABLE
                        return (m_swordstatus);
                    }

                    // Oh well...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                /// <summary>
                /// Process doubleFeedDetection...
                /// </summary>
                /// <returns>our status</returns>
                public SwordStatus ProcessDoublefeeddetection()
                {
                    // Detect doublefeeds...
                    if (m_szTdValue == "on")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_DOUBLEFEEDDETECTION,TWON_ARRAY,TWTY_UINT16,1,0"; // TWDF_ULTRASONIC
                        return (m_swordstatus);
                    }

                    // Don't detect doublefeeds...
                    if (m_szTdValue == "off")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_DOUBLEFEEDDETECTION,TWON_ARRAY,TWTY_UINT16,0"; // (empty array)
                        return (m_swordstatus);
                    }

                    // Oh well...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process imagemerge...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessImagemerge()
                {
	                if (m_szTdValue == "off")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_IMAGEMERGE,TWON_ONEVALUE,TWTY_UINT16,0"; // TWIM_NONE
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "frontAboveRear")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_IMAGEMERGE,TWON_ONEVALUE,TWTY_UINT16,1"; // TWIM_FRONTONTOP
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "frontBelowRear")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_IMAGEMERGE,TWON_ONEVALUE,TWTY_UINT16,2"; // TWIM_FRONTONBOTTOM
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "frontLeftOfRear")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_IMAGEMERGE,TWON_ONEVALUE,TWTY_UINT16,3"; // TWIM_FRONTONLEFT
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "frontRightOfRear")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_IMAGEMERGE,TWON_ONEVALUE,TWTY_UINT16,4"; // TWIM_FRONTONRIGHT
                        return (m_swordstatus);
                    }

                    // No matches...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process noisefilter...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessNoisefilter()
                {
	                if (m_szTdValue == "off")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_NOISEFILTER,TWON_ONEVALUE,TWTY_UINT16,0"; // TWNF_NONE
                        return (m_swordstatus);
                    }

	                if (m_szTdValue == "auto")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_NOISEFILTER,TWON_ONEVALUE,TWTY_UINT16,1"; // TWNF_AUTO
                        return (m_swordstatus);
                    }

	                if (m_szTdValue == "lonePixel")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_NOISEFILTER,TWON_ONEVALUE,TWTY_UINT16,2"; // TWNF_LONEPIXEL
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "majorityRule")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_NOISEFILTER,TWON_ONEVALUE,TWTY_UINT16,3"; // TWNF_MAJORITYRULE
                        return (m_swordstatus);
                    }

                    // Nope...
		            m_swordstatus = SwordStatus.SuccessIgnore;
		            return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process numberOfSheets...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessNumberofsheets()
                {
                    int iNumberofsheets;

                    // Handle the max...
                    if (m_szTdValue == "maximum")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_SHEETCOUNT,TWON_ONEVALUE,TWTY_INT32,0";
                        return (m_swordstatus);
                    }

                    // If we support CAP_SHEETCOUNT, go with that...
                    if (m_processswordtask.GetDeviceRegister().GetTwainInquiryData().GetSheetCount())
                    {
                        if (int.TryParse(m_szTdValue, out iNumberofsheets))
                        {
                            m_aszTwValue = new string[1];
                            m_aszTwValue[0] = "CAP_SHEETCOUNT,TWON_ONEVALUE,TWTY_INT32," + iNumberofsheets;
                            return (m_swordstatus);
                        }
                    }

                    // Otherwise, see what we can do with CAP_XFERCOUNT, we need
                    // to find a way to hook this to CAP_DUPLEXENABLED.  For now
                    // we're just going to assume that we should mulitply it by
                    // 2.  This is okay for flatbeds, they'll just give us the
                    // one image...
                    else
                    {
                        if (int.TryParse(m_szTdValue, out iNumberofsheets))
                        {
                            m_aszTwValue = new string[1];
                            m_aszTwValue[0] = "CAP_XFERCOUNT,TWON_ONEVALUE,TWTY_INT16," + (iNumberofsheets * 2);
                            return (m_swordstatus);
                        }
                    }

                    // Well foo, that didn't work...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process pixelflavor...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessPixelflavor()
                {
	                if (m_szTdValue == "off")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_PIXELFLAVOR,TWON_ONEVALUE,TWTY_UINT16,0"; // TWPF_CHOCOLATE
                        return (m_swordstatus);
                    }

	                if (m_szTdValue == "on")
	                {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_PIXELFLAVOR,TWON_ONEVALUE,TWTY_UINT16,1"; // TWPF_VANILLA
                        return (m_swordstatus);
                    }

                    // Blarg...
		            m_swordstatus = SwordStatus.SuccessIgnore;
		            return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process resolution...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessResolution(SwordValue a_swordvalueHead)
                {
                    int ee;
                    bool blSuccess;
                    int iResolution;
                    string szTdPreviousValue;
                    SwordValue swordvaluePrevious;
                    int[] aiResolution = m_processswordtask.Resolution();

                    // If it's a number, take it...
                    if (int.TryParse(m_szTdValue, out iResolution))
                    {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + iResolution;
                        m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + iResolution;
                        return (SwordStatus.Success);
                    }

                    // Make sure we have our TWAIN data for this scanner...
                    blSuccess = m_processswordtask.LoadTwainListInfo();
                    if (    !blSuccess
                        ||  (aiResolution == null)
                        ||  (aiResolution.Length < 1))
                    {
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    // Get the closest value, higher or lower...
                    if (m_szTdValue == "closest")
	                {
		                // If we're the first item, ignore this value...
		                if (this == a_swordvalueHead)
		                {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            return (m_swordstatus);
                        }

			            // Find the previous value...
			            for (swordvaluePrevious = a_swordvalueHead; swordvaluePrevious.GetNextValue() != this; swordvaluePrevious = swordvaluePrevious.GetNextValue()) ;
                        szTdPreviousValue = swordvaluePrevious.GetValue();

                        // If we're not a number, ignore this value...
			            if (int.TryParse(szTdPreviousValue, out iResolution))
			            {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            return (m_swordstatus);
                        }

				        // If the value we were given is less than the miminum, use the minimum...
				        if (iResolution <= m_processswordtask.Resolution()[0])
				        {
                            m_aszTwValue = new string[2];
                            m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                            m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                            return (SwordStatus.Success);
                        }

                        // If the value we were given is more than the max, use the max...
                        if (iResolution >= m_processswordtask.Resolution()[m_processswordtask.Resolution().Length - 1])
                        {
                            m_aszTwValue = new string[2];
                            m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                            m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                            return (SwordStatus.Success);
                        }

                        // Otherwise, walk the list...
                        for (ee = 0; ee < aiResolution.Length; ee++)
					    {
						    if ((iResolution >= aiResolution[ee]) && (iResolution < aiResolution[ee+1]))
						    {
							    if (iResolution < ((aiResolution[ee] + aiResolution[ee+1]) / 2))
							    {
                                    m_aszTwValue = new string[2];
                                    m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                    m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                    return (SwordStatus.Success);
                                }
                                else
							    {
                                    m_aszTwValue = new string[2];
                                    m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee + 1];
                                    m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee + 1];
                                    return (SwordStatus.Success);
                                }
						    }
					    }

                        // We shouldn't be here...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    // Get the next number higher than us, or the max...
                    if (m_szTdValue == "closestGreaterThan")
	                {
                        // If we're the first item, ignore this value...
                        if (this == a_swordvalueHead)
                        {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            return (m_swordstatus);
                        }                       

                        // Find the previous value...
                        for (swordvaluePrevious = a_swordvalueHead; swordvaluePrevious.GetNextValue() != this; swordvaluePrevious = swordvaluePrevious.GetNextValue()) ;
                        szTdPreviousValue = swordvaluePrevious.GetValue();

                        // If we're not a number, ignore this value...
                        if (int.TryParse(szTdPreviousValue, out iResolution))
                        {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            return (m_swordstatus);
                        }

                        // If the value we were given is less than the miminum, use the minimum...
                        if (iResolution <= aiResolution[0])
                        {
                            m_aszTwValue = new string[2];
                            m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                            m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                            return (SwordStatus.Success);
                        }

                        // If the value we were given is more than the max, use the max...
                        if (iResolution >= aiResolution[aiResolution.Length - 1])
                        {
                            m_aszTwValue = new string[2];
                            m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                            m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                            return (SwordStatus.Success);
                        }

                        // Otherwise, walk the list...
                        for (ee = 0; ee < aiResolution.Length; ee++)
				        {
					        if ((iResolution >= aiResolution[ee]) && (iResolution <= aiResolution[ee+1]))
					        {
                                m_aszTwValue = new string[2];
                                m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                return (SwordStatus.Success);
                            }
				        }

                        // We shouldn't be here...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
	                }

                    // Get the closest value less than us, or the min...
	                if (m_szTdValue == "closestLessThan")
	                {
                        // If we're the first item, ignore this value...
                        if (this == a_swordvalueHead)
                        {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            return (m_swordstatus);
                        }

                        // Find the previous value...
                        for (swordvaluePrevious = a_swordvalueHead; swordvaluePrevious.GetNextValue() != this; swordvaluePrevious = swordvaluePrevious.GetNextValue()) ;
                        szTdPreviousValue = swordvaluePrevious.GetValue();

                        // If we're not a number, ignore this value...
                        if (int.TryParse(szTdPreviousValue, out iResolution))
                        {
                            m_swordstatus = SwordStatus.SuccessIgnore;
                            return (m_swordstatus);
                        }

                        // If the value we were given is less than the miminum, use the minimum...
                        if (iResolution <= aiResolution[0])
                        {
                            m_aszTwValue = new string[2];
                            m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                            m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                            return (SwordStatus.Success);
                        }

                        // If the value we were given is more than the max, use the max...
                        if (iResolution >= aiResolution[aiResolution.Length - 1])
                        {
                            m_aszTwValue = new string[2];
                            m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                            m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                            return (SwordStatus.Success);
                        }

                        // Otherwise, walk the list...
                        for (ee = 0; ee < aiResolution.Length; ee++)
					    {
						    if ((iResolution >= aiResolution[ee]) && (iResolution < aiResolution[ee+1]))
						    {
                                m_aszTwValue = new string[2];
                                m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                return (SwordStatus.Success);
						    }
					    }

                        // We shouldn't be here...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    // Get the max...
                    if (m_szTdValue == "maximum")
	                {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                        m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[aiResolution.Length - 1];
                        return (SwordStatus.Success);
                    }

                    // Get the min...
                    if (m_szTdValue == "minimum")
	                {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                        m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[0];
                        return (SwordStatus.Success);
                    }

                    // Get the optical resolution...
                    if (m_szTdValue == "optical")
	                {
                        // TDB, need this value in the twainlist file...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    // 75dpi or the min...
                    if (m_szTdValue == "preview")
	                {
                        // Loopy...
                        for (ee = 0; ee < aiResolution.Length; ee++)
                        {
                            if (aiResolution[ee] >= 75)
                            {
                                m_aszTwValue = new string[2];
                                m_aszTwValue[0] = "ICAP_XRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                m_aszTwValue[1] = "ICAP_YRESOLUTION,TWON_ONEVALUE,TWTY_FIX32," + aiResolution[ee];
                                return (SwordStatus.Success);
                            }
                        }

                        // We shouldn't be here...
                        m_swordstatus = SwordStatus.SuccessIgnore;
                        return (m_swordstatus);
                    }

                    // Run-roh...
                    m_swordstatus = SwordStatus.SuccessIgnore;
		            return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process orthogonalrotate...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessRotation()
                {
                    if (m_szTdValue == "0")
                    {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_AUTOMATICROTATE,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        m_aszTwValue[1] = "ICAP_ROTATION,TWON_ONEVALUE,TWTY_FIX32,0";
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "90")
                    {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_AUTOMATICROTATE,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        m_aszTwValue[1] = "ICAP_ROTATION,TWON_ONEVALUE,TWTY_FIX32,90";
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "180")
                    {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_AUTOMATICROTATE,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        m_aszTwValue[1] = "ICAP_ROTATION,TWON_ONEVALUE,TWTY_FIX32,180";
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "270")
                    {
                        m_aszTwValue = new string[2];
                        m_aszTwValue[0] = "ICAP_AUTOMATICROTATE,TWON_ONEVALUE,TWTY_BOOL,0"; // FALSE
                        m_aszTwValue[1] = "ICAP_ROTATION,TWON_ONEVALUE,TWTY_FIX32,270";
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "automatic")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "ICAP_AUTOMATICROTATE,TWON_ONEVALUE,TWTY_BOOL,1"; // TRUE
                        return (m_swordstatus);
                    }

                    // Fiddlesticks...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process sheethandling...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessSheethandling()
                {
                    if (m_szTdValue == "normal")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_PAPERHANDLING,TWON_ONEVALUE,TWTY_UINT16,0"; // TWPH_NORMAL
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "fragile")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_PAPERHANDLING,TWON_ONEVALUE,TWTY_UINT16,1"; // TWPH_FRAGILE
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "photograph")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_PAPERHANDLING,TWON_ONEVALUE,TWTY_UINT16,4"; // TWPH_PHOTOGRAPH
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "thick")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_PAPERHANDLING,TWON_ONEVALUE,TWTY_UINT16,2"; // TWPH_THICK
                        return (m_swordstatus);
                    }

                    if (m_szTdValue == "trifold")
                    {
                        m_aszTwValue = new string[1];
                        m_aszTwValue[0] = "CAP_PAPERHANDLING,TWON_ONEVALUE,TWTY_UINT16,3"; // TWPH_TRIFOLD
                        return (m_swordstatus);
                    }

                    // Gee willikers...
                    m_swordstatus = SwordStatus.SuccessIgnore;
                    return (m_swordstatus);
                 }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Process threshold...
                ////////////////////////////////////////////////////////////////////////////////
                public SwordStatus ProcessThreshold()
                {
                    // Just take the value, TWAIN will validate it...
                    m_aszTwValue = new string[1];
                    m_aszTwValue[0] = "ICAP_THRESHOLD,TWON_ONEVALUE,TWTY_FIX32," + m_szTdValue;

	                // All done...
	                return (SwordStatus.Success);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Set the exception for this value....
                ////////////////////////////////////////////////////////////////////////////////
                public void SetException(string a_szException)
                {
	                m_szException = a_szException;
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Set the status for this value....
                ////////////////////////////////////////////////////////////////////////////////
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
	                m_swordstatus = a_swordstatus;
                }

                /// <summary>
                /// Next value in the list, and are we the head?
                /// </summary>
                private SwordValue			m_swordvalueNext;

                /// <summary>
                /// Our main object...
                /// </summary>
                private ProcessSwordTask    m_processswordtask;

                /// <summary>
                /// How we respond...
                /// </summary>
                private SwordTaskResponse	m_swordtaskresponse;

                /// <summary>
                /// Who owns us?
                /// </summary>
                private VendorOwner			m_vendorowner;

                /// <summary>
                /// The status of the item, this includes if it has been sent to
                /// the scanner...
                /// </summary>
                private SwordStatus		    m_swordstatus;

                /// <summary>
                /// The full index of this item in the JSON string...
                /// </summary>
                private string				m_szJsonKey;

                /// <summary>
                /// The exception for this value, null means "ignore"...
                /// </summary>
                private string				m_szException;

                /// <summary>
                /// Vendor UUID, allocated as needed, null means that it's a standard
                /// TWAIN Direct property...
                /// </summary>
                private string				m_szVendor;

                /// <summary>
                /// A single TWAIN Direct value, which we allocate as needed...
                /// </summary>
                private string				m_szTdValue;

                /// <summary>
                /// A TWAIN value of the form container,type,value
                /// </summary>
                private string[]            m_aszTwValue;
            }

            /// <summary>
            /// A capability contains one or more values, which will be tried in order until
            /// the scanner accepts one, or we run out.
            /// </summary>
            sealed class Capability
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
                /// <param name="a_szVendor">the vendor for this item</param>
                public Capability(string a_szCapability, string a_szSwordName, string a_szSwordValue, string a_szException, string a_szJsonIndex, string a_szVendor)
                {
                    // Init value...
                    m_acapabilityvalue = null;

                    // Seed stuff...
                    if ((a_szCapability != null) && (a_szCapability.Length > 0))
                    {
                        m_acapabilityvalue = new CapabilityValue[1];
                        m_acapabilityvalue[0] = new CapabilityValue(a_szCapability, a_szSwordName, a_szSwordValue, a_szException, a_szJsonIndex, a_szVendor);
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
                            a_swordvalue.GetValue(),
                            a_swordvalue.GetException(),
                            a_swordvalue.GetJsonKey(),
                            a_swordvalue.GetVendor()
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
                            a_swordvalue.GetValue(),
                            a_swordvalue.GetException(),
                            a_swordvalue.GetJsonKey(),
                            a_swordvalue.GetVendor()
                        );
                        m_acapabilityvalue = acapabilityvalue;
                    }
                }

                /// <summary>
                /// Set the scanner...
                /// </summary>
                /// <param name="a_twaincstoolkit">toolkit object</param>
                /// <param name="a_szSwordName">the SWORD name we picked</param>
                /// <param name="a_szSwordValue">the SWORD value we picked</param>
                /// <param name="a_szTwainValue">the TWAIN value we picked</param>
                /// <param name="a_szVendor">vendor id</param>
                /// <param name="a_swordtaskresponse">the task response object</param>
                /// <returns></returns>
                public string SetScanner
                (
                    TWAINCSToolkit a_twaincstoolkit,
                    out string a_szSwordName,
                    out string a_szSwordValue,
                    out string a_szTwainValue,
                    string a_szVendor,
                    SwordTaskResponse a_swordtaskresponse
                )
                {
                    int iTryValue;
                    string szStatus;
                    string szTwainValue;
                    TWAIN.STS sts;

                    // Init stuff...
                    szTwainValue = "";
                    a_szSwordName = null;
                    a_szSwordValue = null;
                    a_szTwainValue = null;

                    // Keep trying till we set something...
                    sts = TWAIN.STS.SUCCESS;
                    for (iTryValue = 0; iTryValue < m_acapabilityvalue.Length; iTryValue++)
                    {
                        // Skip stuff that isn't ours...
                        if (    !string.IsNullOrEmpty(m_acapabilityvalue[iTryValue].GetVendor())
                            &&  (m_acapabilityvalue[iTryValue].GetVendor() != a_szVendor))
                        {
                            continue;
                        }

                        // Try to set the value...
                        szStatus = "";
                        szTwainValue = m_acapabilityvalue[iTryValue].GetCapability();
                        sts = a_twaincstoolkit.Send("DG_CONTROL", "DAT_CAPABILITY", "MSG_SET", ref szTwainValue, ref szStatus);
                        if (sts == TWAIN.STS.SUCCESS)
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
                        &&  ((sts == TWAIN.STS.SUCCESS) || (sts == TWAIN.STS.CHECKSTATUS)))
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
                        if ((sts == TWAIN.STS.SUCCESS)
                            || (sts == TWAIN.STS.CHECKSTATUS))
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
                        case "nextAction":
                            if (string.IsNullOrEmpty(szTwainValue))
                            {
                                a_swordtaskresponse.SetError(m_acapabilityvalue[iTryValue].GetException(), m_acapabilityvalue[iTryValue].GetJsonKey(), null, -1);
                            }
                            else
                            {
                                a_swordtaskresponse.SetError(m_acapabilityvalue[iTryValue].GetException(), m_acapabilityvalue[iTryValue].GetJsonKey(), szTwainValue, -1);
                            }
                            return (m_acapabilityvalue[iTryValue].GetException());

                        // Pass the item up...
                        case "nextStream":
                            return (m_acapabilityvalue[iTryValue].GetException());

                        // Pass the item up...
                        case "fail":
                            if (string.IsNullOrEmpty(szTwainValue))
                            {
                                a_swordtaskresponse.SetError(m_acapabilityvalue[iTryValue].GetException(), m_acapabilityvalue[iTryValue].GetJsonKey(), null, -1);
                            }
                            else
                            {
                                a_swordtaskresponse.SetError(m_acapabilityvalue[iTryValue].GetException(), m_acapabilityvalue[iTryValue].GetJsonKey(), szTwainValue, -1);
                            }
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
            sealed class CapabilityValue
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
                /// <param name="a_szVendor">the vendor for this item</param>
                public CapabilityValue(string a_szCapability, string a_szSwordName, string a_szSwordValue, string a_szException, string a_szJsonIndex, string a_szVendor)
                {
                    // Controls...
                    m_szCapability = a_szCapability;
                    m_szSwordName = a_szSwordName;
                    m_szSwordValue = a_szSwordValue;
                    m_szException = a_szException;
                    m_szJsonKey = a_szJsonIndex;
                    m_szVendor = a_szVendor;
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
                /// Return the vendor identification...
                /// </summary>
                /// <returns>the vendor</returns>
                public string GetVendor()
                {
                    return (m_szVendor);
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
                private string m_szVendor;

                #endregion
            }

            /// <summary>
            /// All of the TWAIN values are here, in their capability order...
            /// </summary>
            sealed class TwainCapability
            {
                /// <summary>
                /// Our constructor...
                /// </summary>
                public TwainCapability()
                {
                    // nothing needed at this time...
                }

                /// <summary>
                /// Add a capability...
                /// </summary>
                /// <param name="a_swordsource">we must have a source</param>
                /// <param name="a_swordpixelformat">pixelformat is optional</param>
                /// <param name="a_swordattribute">attribute is optional</param>
                /// <param name="a_swordvalue">value is optional</param>
                /// <param name="a_szTwainCapability">we must have a MSG_SET capability string</param>
                /// <returns></returns>
                public bool Add
                (
                    SwordSource a_swordsource,
                    SwordPixelFormat a_swordpixelformat,
                    SwordAttribute a_swordattribute,
                    SwordValue a_swordvalue,
                    string a_szTwainCapability
                )
                {
                    bool blSuccess = true;
                    CapabilityOrdering capabilityordering;

                    // Dispatch the capability to either the machine (if the cap is less
                    // than CAP_CAMERASIDE) or to the source/pixelformat topology in all
                    // other cases.  If a spot is already in use, discard the item based
                    // on the exception...

                    // we have to have a source and a TWAIN Capability...
                    if ((a_swordsource == null) || string.IsNullOrEmpty(a_szTwainCapability))
                    {
                        return (false);
                    }

                    // We must be able to extract the TWAIN capability...
                    string[] asz = a_szTwainCapability.Split(new char[] { ',' });
                    if ((asz == null) || (asz.Length == 0) || string.IsNullOrEmpty(asz[0]))
                    {
                        goto ABORT;
                    }
                    if (!Enum.TryParse<CapabilityOrdering>(asz[0], out capabilityordering))
                    {
                        goto ABORT;
                    }

                    // This is a machine value...
                    if (capabilityordering < CapabilityOrdering.CAP_CAMERASIDE)
                    {
                        // We need one of these...
                        if (m_acapabilitymapMachine == null)
                        {
                            m_acapabilitymapMachine = new CapabilityMap[(int)CapabilityOrdering.Length];
                        }

                        // Check if there's already a chicken in the coop...
                        if (m_acapabilitymapMachine[(int)capabilityordering] != null)
                        {
                            goto ABORT;
                        }

                        // Save it...
                        m_acapabilitymapMachine[(int)capabilityordering].m_swordsource = a_swordsource;
                        m_acapabilitymapMachine[(int)capabilityordering].m_swordpixelformat = a_swordpixelformat;
                        m_acapabilitymapMachine[(int)capabilityordering].m_swordattribute = a_swordattribute;
                        m_acapabilitymapMachine[(int)capabilityordering].m_swordvalue = a_swordvalue;
                        m_acapabilitymapMachine[(int)capabilityordering].m_szTwainCapability = a_szTwainCapability;

                        // All done...
                        return (true);
                    }

                    // Get our topology...


                    // We only have a source (we know we got that, see above)...
                    if (a_swordpixelformat == null)
                    {
                    }

                    // We only have a pixelFormat...
                    if ((a_swordattribute == null) || (a_swordvalue == null))
                    {
                    }

                    // We've got it all...

                    // All done...
                    return (true);

                    // Abort...
                    ABORT:

                    // We have a value...
                    if (a_swordvalue != null)
                    {
                    }

                    // We have an attribute...
                    if (a_swordvalue != null)
                    {
                    }

                    // We have a pixelFormat...
                    if (a_swordvalue != null)
                    {
                    }

                    // We'd better have a source (see above)...

                    // All done...
                    return (blSuccess);
                }

                /// <summary>
                /// All of the capabilities in capability order (mapped through the
                /// CapabilityOrdering enumeration).  We fill in this stuff in a first
                /// come first serve mode.  However, when we run it, we'll do the
                /// flatbed before the ADF, so the ADFs values will take if the scanner
                /// can't handle separate values for both...
                /// </summary>
                private CapabilityMap[] m_acapabilitymapMachine;
                //private CapabilityMap[] m_acapabilitymapFlatbedBw1;
                //private CapabilityMap[] m_acapabilitymapFlatbedGray8;
                //private CapabilityMap[] m_acapabilitymapFlatbedRgb24;
                //private CapabilityMap[] m_acapabilitymapFrontBw1;
                //private CapabilityMap[] m_acapabilitymapFrontGray8;
                //private CapabilityMap[] m_acapabilitymapFrontRgb24;
                //private CapabilityMap[] m_acapabilitymapRearBw1;
                //private CapabilityMap[] m_acapabilitymapRearGray8;
                //private CapabilityMap[] m_acapabilitymapRearRgb24;
            }

            /// <summary>
            /// Each encryptionProfile provides a name that the scanner uses to look up
            /// encryption profile actions in its firmware...
            /// </summary>
            sealed class SwordEncryptionProfile
            {
                /// <summary>
                /// Constructor...
                /// </summary>
                /// <param name="a_swordtaskresponse">object for the response</param>
                /// <param name="a_swordencryptionprofileHead">head of the encryptionProfile or null</param>
                /// <param name="a_szJsonKey">actions[].encryptionprofiles[]</param>
                /// <param name="a_szEncryptionProfileName">name of the encryptionProfile</param>
                /// <param name="a_szException">exception for this encryptionProfile</param>
                /// <param name="a_szVendor">vendor id, if any</param>
                /// <param name="a_szEncryptionProfileProfile">profile of the encryptionProfile</param>
                public SwordEncryptionProfile
                (
                    ProcessSwordTask a_processsowrdtask,
                    SwordTaskResponse a_swordtaskresponse,
                    SwordEncryptionProfile a_swordencryptionprofileHead,
                    string a_szJsonKey,
                    string a_szEncryptionProfileName,
                    string a_szException,
                    string a_szVendor,
                    string a_szEncryptionProfileProfile
                )
                {
                    // If the vendor isn't us, then skip it, this isn't subject to exceptions,
                    // so we return right away...
                    m_vendorowner = a_processsowrdtask.GetVendorOwner(a_szVendor);
                    if (m_vendorowner == VendorOwner.Unknown)
                    {
                        m_swordstatus = SwordStatus.VendorMismatch;
                        return;
                    }

                    // Init stuff...
                    m_processswordtask = a_processsowrdtask;
                    m_swordtaskresponse = a_swordtaskresponse;
                    m_swordstatus = SwordStatus.Success;
                    m_szJsonKey = a_szJsonKey;
                    m_szEncryptionProfileName = a_szEncryptionProfileName;
                    m_szException = a_szException;
                    m_szVendor = a_szVendor;
                    m_szEncryptionProfileProfile = a_szEncryptionProfileProfile;

                    // If we didn't get an exception, or if the exception is
                    // the nextaction placeholder, then assign the nextencrptionprofile
                    // placeholder...
                    if (string.IsNullOrEmpty(m_szException) || (m_szException == "@nextActionOrIgnore"))
                    {
                        m_szException = "@nextEncryptionProfileOrIgnore";
                    }

                    // We're the head of the list...
                    if (a_swordencryptionprofileHead == null)
                    {
                        // nothing needed...
                    }

                    // We're being appended to the list...
                    else
                    {
                        SwordEncryptionProfile swordencryptionprofileParent;
                        for (swordencryptionprofileParent = a_swordencryptionprofileHead; swordencryptionprofileParent.m_swordencryptionprofileNext != null; swordencryptionprofileParent = swordencryptionprofileParent.m_swordencryptionprofileNext) ;
                        swordencryptionprofileParent.m_swordencryptionprofileNext = this;
                    }
                }

                /// <summary>
                /// Build the task reply...
                /// </summary>
                /// <returns>true on success</returns>
                public bool BuildTaskReply()
                {
                    // Only report on success...
                    if (m_swordstatus != SwordStatus.Success)
                    {
                        return (true);
                    }

                    // Start of the encryption profile...
                    m_swordtaskresponse.JSON_OBJ_BGN(4, "");

                    // The vendor (if any) and the stream's name...
                    if (m_vendorowner == VendorOwner.Scanner) m_swordtaskresponse.JSON_STR_SET(5, "vendor", ",", m_szVendor);
                    m_swordtaskresponse.JSON_STR_SET(5, "name", ",", m_szEncryptionProfileName);

                    // End of the  encryption profile...
                    m_swordtaskresponse.JSON_OBJ_END(4, ",");

                    // All done...
                    return (true);
                }

                /// <summary>
                /// Get the name of the encryptionProfile...
                /// </summary>
                /// <returns>the name</returns>
                public string GetName()
                {
                    return (m_szEncryptionProfileName);
                }

                /// <summary>
                /// Get the profile of the encryptionProfile...
                /// </summary>
                /// <returns>the name</returns>
                public string GetProfile()
                {
                    return (m_szEncryptionProfileProfile);
                }

                /// <summary>
                /// Get the next encryption profile in the list...
                /// </summary>
                /// <returns>the next encryptionProfile or null</returns>
                public SwordEncryptionProfile GetNextEncryptionProfile()
                {
                    return (m_swordencryptionprofileNext);
                }

                /// <summary>
                /// Get the exception for this stream...
                /// </summary>
                /// <returns>th exception</returns>
                public string GetException()
                {
                    return (m_szException);
                }

                /// <summary>
                /// Get the status for this stream...
                /// </summary>
                /// <returns></returns>
                public SwordStatus GetSwordStatus()
                {
                    return (m_swordstatus);
                }

                /// <summary>
                /// Get the vendor for this stream...
                /// </summary>
                /// <returns></returns>
                public string GetVendor()
                {
                    return (m_szVendor);
                }

                /// <summary>
                /// Process this encryption profile...
                /// </summary>
                /// <returns>the status of processing</returns>
                public SwordStatus Process()
                {
                    // Assume success...
                    m_swordstatus = SwordStatus.Run;

                    // Make sure we have a name...
                    if (string.IsNullOrEmpty(m_szEncryptionProfileName))
                    {
                        int szIndex;
                        szIndex = m_szJsonKey.LastIndexOf("[");
                        if (szIndex != -1)
                        {
                            m_szEncryptionProfileName = "encryptionprofile" + m_szJsonKey.Substring(szIndex + 1);
                            szIndex = m_szEncryptionProfileName.LastIndexOf("]");
                            if (szIndex != -1)
                            {
                                m_szEncryptionProfileName = m_szEncryptionProfileName.Remove(szIndex);
                            }
                        }
                    }

                    // All done...
                    return (m_swordstatus);
                }

                /// <summary>
                /// Set a task error...
                /// </summary>
                /// <param name="a_szException">the exception  we're processing</param>
                /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
                /// <param name="a_szCode">the error code</param>
                /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
                /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
                public void SetError
                (
                    string a_szException,
                    string a_szJsonExceptionKey,
                    string a_szCode,
                    long a_lJsonErrorIndex
                )
                {
                    m_swordtaskresponse.SetError(a_szException, a_szJsonExceptionKey, a_szCode, a_lJsonErrorIndex);
                }

                /// <summary>
                /// Set the exception for this encryption profile...
                /// </summary>
                /// <param name="a_szException"></param>
                public void SetException(string a_szException)
                {
                    m_szException = a_szException;
                }

                ////////////////////////////////////////////////////////////////////////////////
                //	Description:
                //		Set the status for this stream...
                ////////////////////////////////////////////////////////////////////////////////
                public void SetSwordStatus(SwordStatus a_swordstatus)
                {
                    m_swordstatus = a_swordstatus;
                }

                /// <summary>
                /// Next one in the list, not if we're the head...
                /// </summary>
                private SwordEncryptionProfile m_swordencryptionprofileNext;

                /// <summary>
                /// Our main object...
                /// </summary>
                private ProcessSwordTask m_processswordtask;

                /// <summary>
                /// Our response...
                /// </summary>
                private SwordTaskResponse m_swordtaskresponse;

                /// <summary>
                /// Who owns us?
                /// </summary>
                private VendorOwner m_vendorowner;

                /// <summary>
                /// The status of the item...
                /// </summary>
                private SwordStatus m_swordstatus;

                /// <summary>
                /// The name of the encryption profile...
                /// </summary>
                private string m_szEncryptionProfileName;

                /// <summary>
                /// The name of the encryption profile's profile...
                /// </summary>
                private string m_szEncryptionProfileProfile;

                /// <summary>
                /// The index of this item in the JSON string...
                /// </summary>
                private string m_szJsonKey;

                /// <summary>
                /// The default exception for this stream...
                /// </summary>
                private string m_szException;

                /// <summary>
                /// Vendor UUID...
                /// </summary>
                private string m_szVendor;
            }

            #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// A task object (one that we're processing)...
        /// </summary>
        private SwordTask m_swordtask;

        /// <summary>
        /// The JSON lookup object for the task...
        /// </summary>
        private JsonLookup m_jsonlookupTask;

        /// <summary>
        /// The task response...
        /// </summary>
        private SwordTaskResponse m_swordtaskresponse;

        /// <summary>
        /// We use this to match a source/pixelFormat combination to
        /// the streamName/sourceName/pixelFormatName values that were
        /// provided in the task, or their defaults if they're not
        /// specified...
        /// </summary>
        private ConfigureNameLookup m_configurenamelookup;

        /// <summary>
        /// Information about our TWAIN driver...
        /// </summary>
        private DeviceRegister m_deviceregister;

        /// <summary>
        /// Capability ordering for TWAIN (as vendor specific content)
        /// </summary>
        //private TwainCapabilityOrdering m_twaincapabilityordering;

        /// <summary>
        /// The TWAIN Direct vendor id...
        /// </summary>
        string m_szVendorTwainDirect;

        /// <summary>
        /// The scanner's vendor id...
        /// </summary>
        string m_szVendor;

        /// <summary>
        /// The data from the twainlist file that we originally
        /// generated that was sent back to use by TwainDirect.Scanner,
        /// but just for the scanner we want to use...
        /// </summary>
        private JsonLookup m_jsonlookupTwidentity;

        /// <summary>
        /// An array of supported resolutions gleaned from ms_jsonlookupTwidentity...
        /// </summary>
        private int[] m_aiResolution;

        /// <summary>
        /// TWAIN: the level of support this driver has for TWWAIN Direct...
        /// </summary>
        private DeviceRegister.TwainInquiryData m_twaininquirydata;

        /// <summary>
        /// TWAIN: true if we can detect paper in the feeder...
        /// </summary>
        private bool m_blPaperDetectable;

        /// <summary>
        /// TWAIN: the toolkit used to talk to the scanner, the one
        /// for the caller is passed to us, if we get it we assign
        /// it to the other one...
        /// </summary>
        private TWAINCSToolkit m_twaincstoolkit;
        private TWAINCSToolkit m_twaincstoolkitCaller;

        /// <summary>
        /// TWAIN: Identity of our TWAIN driver...
        /// </summary>
        private string m_szTwainDriverIdentity;

        /// <summary>
        /// TWAIN: The owner of this scanner...
        /// </summary>
        private string m_szVendorOwner;       

        /// <summary>
        /// TWAIN: true if ICAP_AUTOMATICCOLORENABLED is supported...
        /// </summary>
        private bool m_blAutomaticColorEnabled;

        /// <summary>
        /// TWAIN: true if we're processing...
        /// </summary>
        private bool m_blProcessing;

        /// <summary>
        /// TWAIN: CAP_AUTOMATICSENSEMEDIUM...
        /// </summary>
        private Capability m_capabilityAutomaticsensemedium;
        private bool m_blAutomaticSenseMedium;

        /// <summary>
        /// TWAIN: CAP_DUPLEXENABLED, the m_blDuplex value tells us what
        /// we think we did for duplex vs simplex scanning...
        /// </summary>
        private Capability m_capabilityDuplexenabled;
        private bool m_blDuplexEnabled;
        private bool m_blDuplex;

        /// <summary>
        /// TWAIN: CAP_FEEDERENABLED, the m_blFlatbed value tells us what
        /// we think we used for scanning so we can report it in the metadata...
        /// </summary>
        private Capability m_capabilityFeederenabled;
        private bool m_blFeederEnabled;
        private bool m_blFlatbed;

        /// <summary>
        /// Place where we write stuff...
        /// </summary>
        private string m_szImagesFolder;

        /// <summary>
        /// If non-null, points to the encryptionProfile we should use
        /// to encrypt images when we scan...
        /// </summary>
        private string m_szEncryptionProfileName;

        #endregion
    }

    #endregion


    /// <summary>
    /// Track the data needed for the response to a task...
    /// </summary>
    #region SwordTaskResponse

    // Our class...
    internal sealed class SwordTaskResponse
    {
        #region Public Methods

        /// <summary>
        /// Init stuff...
        /// </summary>
        public SwordTaskResponse(ProcessSwordTask a_processswordtask)
        {
            // non-zero stuff...
            Clear();
            m_processswordtask = a_processswordtask;


            // Pack our JSON output...
            m_blPack = (Config.Get("packJson", "true") == "true");
        }

        /// <summary>
        /// Append task response...
        /// </summary>
        /// <param name="a_szTaskResponse">data to append</param>
        public void AppendTaskResponse(string a_szTaskResponse)
        {
            m_szTaskResponse += a_szTaskResponse;
        }

        /// <summary>
        /// Clear any current error information...
        /// </summary>
        public void Clear()
        {
            // Leave the task response buffer alone...
            m_blSuccess = true;
            m_lJsonErrorIndex = -1;
            m_szException = "";
            m_szJsonExceptionKey = "";
            m_szLexiconValue = "";
            m_szTaskResponse = "";
        }

        /// <summary>
        /// Get the task response...
        /// </summary>
        /// <returns>the task response</returns>
        public string GetTaskResponse()
        {
            return (m_szTaskResponse);
        }

        /// <summary>
        /// Set a task error...
        /// </summary>
        /// <param name="a_jsonlookup">json for the task, used to look up stuff</param>
        /// <param name="a_szException">the exception  we're processing</param>
        /// <param name="a_szJsonExceptionKey">JSON key where the error occurred (if applicable)</param>
        /// <param name="a_szCode">the error code</param>
        /// <param name="a_lJsonErrorIndex">character offset where the error occurred (if applicable)</param>
        /// <param name="a_blAddActionsArray">embed the error in an actions array</param>
        public void SetError
        (
            string a_szException,
            string a_szJsonExceptionKey,
            string a_szCode,
            long a_lJsonErrorIndex,
            bool a_blAddActionsArray = false
        )
        {
            string szAction = "";

            // Ruh-roh, we've already done this...
            if (!m_blSuccess)
            {
                // We can't log anything here, it would be too noisy...
                return;
            }

            // Record the error...
            Clear();
            m_blSuccess = false;
            m_szException = a_szException;
            m_szJsonExceptionKey = a_szJsonExceptionKey;
            m_szLexiconValue = a_szCode;
            m_lJsonErrorIndex = a_lJsonErrorIndex;

            // Attempt to get an action...
            JsonLookup jsonlookupTask = m_processswordtask.GetJsonlookupTask();
            if ((jsonlookupTask != null) && !string.IsNullOrEmpty(a_szJsonExceptionKey))
            {
                string[] asz = a_szJsonExceptionKey.Split(new char[] { '.' });
                if ((asz != null) && (asz.Length > 0))
                {
                    szAction = jsonlookupTask.Get(asz[0] + ".action");
                }
            }

            // We're working with something that has no access to an action array...
            if (a_blAddActionsArray)
            {
                JSON_OBJ_BGN(0, "");                                                // start root
                JSON_ARR_BGN(1, "actions");                                         // start actions array
            }

            // Handle a JSON error...
            if (string.IsNullOrEmpty(a_szCode) || (a_szCode == "invalidJson"))
            {
                JSON_OBJ_BGN(2, "");                                                 // start action object
                JSON_STR_SET(3, "action", ",", szAction);                            // action property,
                JSON_OBJ_BGN(3, "results");                                          // start results object
                JSON_TOK_SET(4, "success", ",", "false");                            // success property,
                JSON_STR_SET(4, "code", ",", "invalidJson");                         // code property,
                JSON_NUM_SET(4, "characterOffset", "", (int)m_lJsonErrorIndex);      // characterOffset property
                JSON_OBJ_END(3, "");                                                 // end response object
            }

            // If it's an invalidTask or an invalidValue, then include the jsonKey...
            else if ((a_szCode == "invalidTask") || (a_szCode == "invalidValue"))
            {
                JSON_OBJ_BGN(2, "");                                                 // start action object
                JSON_STR_SET(3, "action", ",", szAction);                            // action property,
                JSON_OBJ_BGN(3, "results");                                          // start results object
                JSON_TOK_SET(4, "success", ",", "false");                            // success property,
                JSON_STR_SET(4, "code", ",", a_szCode);                              // code property
                JSON_STR_SET(4, "jsonKey", "", m_szJsonExceptionKey);                // jsonKey property
                JSON_OBJ_END(3, "");                                                 // end response object
                JSON_OBJ_END(2, "");                                                 // end action object
            }

            // Anything else comes here...
            else
            {
                JSON_OBJ_BGN(2, "");                                                 // start action object
                JSON_STR_SET(3, "action", ",", szAction);                            // action property,
                JSON_OBJ_BGN(3, "results");                                          // start results object
                JSON_TOK_SET(4, "success", ",", "false");                            // success property,
                JSON_STR_SET(4, "code", ",", a_szCode);                              // code property,
                JSON_OBJ_END(3, "");                                                 // end response object
                JSON_OBJ_END(2, "");                                                 // end action object
            }

            // We're working with something that has no access to an action array...
            if (a_blAddActionsArray)
            {
                JSON_ARR_END(1, "");                                                // end actions array
                JSON_OBJ_END(0, "");                                                // end root
            }
        }

        /// <summary>
        /// Set the pack flag...
        /// </summary>
        /// <param name="a_blPack"></param>
        public void SetPack
        (
            bool a_blPack
        )
        {
            m_blPack = a_blPack;
        }

        /// <summary>
        /// Set the task response...
        /// </summary>
        /// <param name="a_szTaskResponse">the reply</param>
        public void SetTaskResponse(string a_szTaskResponse)
        {
            m_szTaskResponse = a_szTaskResponse;
        }

        /// <summary>
        /// A standalone newline marker...
        /// </summary>
        public void JSONEOLN()
        {
            m_szTaskResponse += (m_blPack ? "" : Environment.NewLine);
        }

        /// <summary>
        /// Clear the response...
        /// </summary>
        public void JSON_CLEAR()
        {
            m_szTaskResponse = "";
        }

        /// <summary>
        /// Root begin...
        /// </summary>
        public void JSON_ROOT_BGN()
        {
            m_szTaskResponse = m_blPack ? "{" : ("{" + Environment.NewLine);
        }

        /// <summary>
        /// Root end...
        /// </summary>
        public void JSON_ROOT_END()
        {
            // Remove the comma from the previous line...
            if (m_szTaskResponse.EndsWith(",") || m_szTaskResponse.EndsWith("," + Environment.NewLine))
            {
                m_szTaskResponse = m_szTaskResponse.Remove(m_szTaskResponse.LastIndexOf(","));
                if (!m_blPack) m_szTaskResponse += Environment.NewLine;
            }
            m_szTaskResponse += "}";
        }

        /// <summary>
        /// Array begin...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szName">array name</param>
        public void JSON_ARR_BGN(int a_iTab, string a_szName)
        {
            m_szTaskResponse += !string.IsNullOrEmpty(a_szName) ? (m_blPack ? ("\"" + a_szName + "\":[") : (Tabs(a_iTab) + "\"" + a_szName + "\": [" + Environment.NewLine)) : (m_blPack ? "[" : (Tabs(a_iTab) + "[" + Environment.NewLine));
        }

        /// <summary>
        /// Array end...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szComma">comma or empty string</param>
        public void JSON_ARR_END(int a_iTab, string a_szComma)
        {
            // Remove the comma from the previous line...
            if (m_szTaskResponse.EndsWith(",") || m_szTaskResponse.EndsWith("," + Environment.NewLine))
            {
                m_szTaskResponse = m_szTaskResponse.Remove(m_szTaskResponse.LastIndexOf(","));
                if (!m_blPack) m_szTaskResponse += Environment.NewLine;
            }
            m_szTaskResponse += m_blPack ? ("]" + a_szComma) : (Tabs(a_iTab) + "]" + a_szComma + Environment.NewLine);
        }

        /// <summary>
        /// Object begin...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szName">object name</param>
        public void JSON_OBJ_BGN(int a_iTab, string a_szName)
        {
            m_szTaskResponse += !string.IsNullOrEmpty(a_szName) ? (m_blPack ? ("\"" + a_szName + "\":{") : (Tabs(a_iTab) + "\"" + a_szName + "\": {" + Environment.NewLine)) : (m_blPack ? "{" : (Tabs(a_iTab) + "{" + Environment.NewLine));
        }

        /// <summary>
        /// Object end...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szComma">comma or empty string</param>
        public void JSON_OBJ_END(int a_iTab, string a_szComma)
        {
            // Remove the comma from the previous line...
            if (m_szTaskResponse.EndsWith(",") || m_szTaskResponse.EndsWith("," + Environment.NewLine))
            {
                m_szTaskResponse = m_szTaskResponse.Remove(m_szTaskResponse.LastIndexOf(","));
                if (!m_blPack) m_szTaskResponse += Environment.NewLine;
            }
            m_szTaskResponse += m_blPack ? ("}" + a_szComma) : (Tabs(a_iTab) + "}" + a_szComma + Environment.NewLine);
        }

        /// <summary>
        /// Strings...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szName">string name</param>
        /// <param name="a_szComma">comma or empty string</param>
        /// <param name="a_szStr">value</param>
        public void JSON_STR_SET(int a_iTab, string a_szName, string a_szComma, string a_szStr)
        {
            m_szTaskResponse += m_blPack ? ("\"" + a_szName + "\":\"" + a_szStr + "\"" + a_szComma) : (Tabs(a_iTab) + "\"" + a_szName + "\": \"" + a_szStr + "\"" + a_szComma + Environment.NewLine);
        }

        /// <summary>
        /// Tokens...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szName">token name</param>
        /// <param name="a_szComma">comma or empty string</param>
        /// <param name="a_szStr">value</param>
        public void JSON_TOK_SET(int a_iTab, string a_szName, string a_szComma, string a_szStr)
        {
            m_szTaskResponse += m_blPack ? ("\"" + a_szName + "\":" + a_szStr + a_szComma) : (Tabs(a_iTab) + "\"" + a_szName + "\": " + a_szStr + a_szComma + Environment.NewLine);
        }

        /// <summary>
        /// Numbers...
        /// </summary>
        /// <param name="a_iTab">depth</param>
        /// <param name="a_szName">token name</param>
        /// <param name="a_szComma">comma or empty string</param>
        /// <param name="a_iNum">value</param>
        public void JSON_NUM_SET(int a_iTab, string a_szName, string a_szComma, int a_iNum)
        {
            m_szTaskResponse += m_blPack ? ("\"" + a_szName + "\":" + a_iNum + a_szComma) : (Tabs(a_iTab) + "\"" + a_szName + "\": " + a_iNum + a_szComma + Environment.NewLine);
        }

        #endregion


        /// <summary>
        /// Private Methods
        /// </summary>
        #region Private Methods

        // Generate tabs...
        private string Tabs(int a_iTotal)
        {
            if (a_iTotal <= 0)
            {
                return ("");
            }
            return (new string('\t', a_iTotal));
        }

        #endregion


        /// <summary>
        /// Private Definitions
        /// </summary>
        #region Private Definitions

        // TWAIN Direct lookup stuff, map TWAIN Direct attributes to
        // their corresponding TWAIN capabilities, if we have one...
        static readonly string[,] s_atwaindirectlookup = new string[,]
        {
            { "alarms",                             "CAP_ALARMS" },
            { "alarmVolume",                        "CAP_ALARMVOLUME" },
            { "automaticDeskew",                    "CAP_AUTOMATICDESKEW" },
            { "automaticSize",                      "CAP_AUTOSIZE" },
            { "barcodes",                           "ICAP_BARCODEDETECTIONENABLED" },
            { "bitDepthReduction",                  "ICAP_BITDEPTHREDUCTION" },
            { "brightness",                         "ICAP_BRIGHTNESS" },
            { "compression",                        "ICAP_COMPRESSION" },
            { "continuousScan",                     "CAP_AUTOSCAN" },
            { "contrast",                           "ICAP_CONTRAST" },
            { "cropping",                           "ICAP_AUTOMATICBORDERDETECTION" },
            { "discardBlankImages",                 "ICAP_AUTODISCARDBLANKPAGES" },
            { "doubleFeedDetection",                "CAP_DOUBLEFEEDDETECTION" },
            { "doubleFeedDetectionLength",          "CAP_DOUBLEFEEDDETECTIONLENGTH" },
            { "doubleFeedDetectionResponse",        "CAP_DOUBLEFEEDDETECTIONRESPONSE" },
            { "doubleFeedDetectionSensitivity",     "CAP_DOUBLEFEEDDETECTIONSENSITIVITY" },
            { "flipRotation",                       "ICAP_FLIPROTATION" },
            { "height",                             "ICAP_FRAME" },
            { "imageMerge",                         "ICAP_IMAGEMERGE" },
            { "imageMergeHeightThreshold",          "ICAP_IMAGEMERGEHEIGHTTHREADHOLD" },
            { "invert",                             "ICAP_PIXELFLAVOR" },
            { "jpegQuality",                        "ICAP_JPEGQUALITY" },
            { "micr",                               "CAP_MICRENABLED" },
            { "mirror",                             "ICAP_MIRROR" },
            { "noiseFilter",                        "ICAP_NOISEFILTER" },
            { "overScan",                           "ICAP_OVERSCAN" },
            { "numberOfSheets",                     "CAP_SHEETCOUNT" },
            { "offsetX",                            "ICAP_FRAME" },
            { "offsetY",                            "ICAP_FRAME" },
            { "patchCodes",                         "ICAP_PATCHCODEDETECTIONENABLED" },
            { "resolution",                         "CAP_XRESOLUTION" },
            { "rotation",                           "ICAP_ROTATION" },
            { "sheetHandling",                      "CAP_FEEDERMODE" },
            { "sheetSize",                          "ICAP_SUPPORTEDSIZES" },
            { "threshold",                          "ICAP_THRESHOLD" },
            { "uncalibratedImage",                  "xxx" },
            { "width",                              "ICAP_FRAME" },
	        // Must be last...
	        { "",                                   "" }
        };

        #endregion


        /// <summary>
        /// Private Attributes
        /// </summary>
        #region Private Attributes

        /// <summary>
        /// True as long as the task is successful, we anticipate success, so that's
        /// our starting point.  If this value goes to false, then further processing
        /// must stop, and the error info at the point of failure is returned...
        /// </summary>
        private bool m_blSuccess;

        /// <summary>
        /// The exception we're reporting on failure...
        /// </summary>
        private string m_szException;

        /// <summary>
        /// The location in the task, returned in the dotted key notation
        /// (ex: action[0].stream[0].source[0]
        /// </summary>
        private string m_szJsonExceptionKey;

        /// <summary>
        /// Lexicon value that we used...
        /// </summary>
        private string m_szLexiconValue;

        /// <summary>
        /// If the JSON can't be parsed, this value will (hopefully) point to where
        /// the error occurred...
        /// </summary>
        private long m_lJsonErrorIndex;

        /// <summary>
        /// Our task reply buffer, and its size...
        /// </summary>
        private string m_szTaskResponse;

        /// <summary>
        /// Pack the data in our response...
        /// </summary>
        private bool m_blPack;

        /// <summary>
        /// Access to the root of the task...
        /// </summary>
        private ProcessSwordTask m_processswordtask;

        #endregion
    }

    #endregion
}
