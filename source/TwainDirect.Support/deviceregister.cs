///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.DeviceRegister
//
// Container for device information, both ephemeral and persistant.  This class is
// only used by TwainLocalScanner.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    31-Oct-2014     Initial Release
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
using System.IO;
using System.Net;

namespace TwainDirect.Support
{
    /// <summary>
    /// The device register.  We use this to squirrel away data about the devices
    /// that we are either creating or accessing.  It has a context, so that we
    /// can identify a current device and work with that...
    /// </summary>
    public sealed class DeviceRegister
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Initialize stuff...
        /// </summary>
        public DeviceRegister()
        {
            m_device = default(Device);
        }

        /// <summary>
        /// Clear the device registration...
        /// </summary>
        public void Clear()
        {
            m_device = default(Device);
        }

        /// <summary>
        /// Information about our TWAIN driver...
        /// </summary>
        /// <returns>TWAIN driver info</returns>
        public TwainInquiryData GetTwainInquiryData()
        {
            return (m_device.twaininquirydata);
        }

        /// <summary>
        /// Get the contents of the register.txt file...
        /// </summary>
        /// <returns>everything we know about the scanner</returns>
        public string GetTwainLocalScanner()
        {
            return (m_device.szScanner);
        }

        /// <summary>
        /// What level of support do we have in the TWAIN driver?
        /// </summary>
        /// <returns>level of support</returns>
        public TwainDirectSupport GetTwainLocalTwainDirectSupport()
        {
            if (m_device.twaininquirydata == null)
            {
                return (TwainDirectSupport.None);
            }
            return (m_device.twaininquirydata.GetTwainDirectSupport());
        }

        /// <summary>
        /// Return the TWAIN ty= field...
        /// </summary>
        /// <returns>the access token</returns>
        public string GetTwainLocalTy()
        {
            return (m_device.szTwainLocalTy);
        }

        /// <summary>
        /// Return the note= field (supplied by the user)...
        /// </summary>
        /// <returns>the note</returns>
        public string GetTwainLocalNote()
        {
            return (m_device.szTwainLocalNote);
        }

        /// <summary>
        /// Return the TWAIN Local instance name...
        /// </summary>
        /// <returns>instance name</returns>
        public string GetTwainLocalInstanceName()
        {
            return (m_device.szTwainLocalInstanceName);
        }

        /// <summary>
        /// Load data from a file or from the argument...
        /// </summary>
        /// <param name="a_szFile">the file or the data itself</param>
        /// <returns>try if successful</returns>
        public bool Load(string a_szFileOrData)
        {
            try
            {
                string szJson;

                // Validate...
                if (string.IsNullOrEmpty(a_szFileOrData))
                {
                    Log.Error("DeviceRegister.Load failed...no data...");
                    return (false);
                }

                // If the data contains a '{' then we think we have
                // data instead of a file...
                if (a_szFileOrData.Contains("{"))
                {
                    szJson = a_szFileOrData;
                }

                // Otherwise, we expect a file...
                else
                {
                    // No joy...
                    if (!File.Exists(a_szFileOrData))
                    {
                        Log.Error("DeviceRegister.Load failed...no file <" + a_szFileOrData + ">");
                        return (false);
                    }

                    // Read it...
                    szJson = File.ReadAllText(a_szFileOrData);

                    // Bugger...
                    if (string.IsNullOrEmpty(szJson))
                    {
                        Log.Error("DeviceRegister.Load failed...no data <" + a_szFileOrData + ">");
                        return (false);
                    }
                }

                // Parse it...
                long lResponseCharacterOffset;
                JsonLookup jsonlookup = new JsonLookup();
                jsonlookup.Load(szJson, out lResponseCharacterOffset);

                // Start with a clean slate...
                m_device = default(Device);

                // Add the entry...
                Set
                (
                    jsonlookup.Get("scanner.twainLocalTy"),
                    jsonlookup.Get("scanner.twainLocalSerialNumber"),
                    jsonlookup.Get("scanner.twainLocalNote"),
                    jsonlookup.Get("scanner"),
                    jsonlookup.Get("scanner.twainLocalScanner")
                );
            }
            catch
            {
                m_device = default(Device);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Persist the data to a file...
        /// </summary>
        /// <param name="a_szFile">the file to save the data in</param>
        /// <returns>true if successful</returns>
        public bool Save(string a_szFile)
        {
            string szData = "";

            // Extra protection...
            try
            {
                // Clear the file...
                if (File.Exists(a_szFile))
                {
                    File.Delete(a_szFile);
                }

                // Root JSON object...
                szData += "{";

                // Scanner data...
                szData += "\"scanner\":{";

                // Persist the items we want to remember for the user, technically we
                // shouldn't hold onto the serial number, because user's might move
                // scanners around, but it's so expensive to get the value (in terms
                // of performance) that we'l going to take the risk...
                szData += "\"twainLocalTy\":\"" + m_device.szTwainLocalTy + "\",";
                szData += "\"twainLocalSerialNumber\":\"" + m_device.szTwainLocalSerialNumber + "\",";
                szData += "\"twainLocalNote\":\"" + m_device.szTwainLocalNote + "\",";
                szData += "\"twainLocalScanner\": " + m_device.szScanner;

                // End of scanner object...
                szData += "}";

                // End of root object...
                szData += "}";

                // Save to the file...
                File.WriteAllText(a_szFile, szData);
            }
            catch
            {
                return (false);
            }

            // All done...
            return (true);
        }

        /// <summary>
        /// Add a device or modify the contents of an existing device.  We
        /// add data in bits and pieces, so expect to see this call made
        /// more than once.  We use two keys: the device name and the device
        /// id...
        /// </summary>
        /// <param name="szTwainLocalTy">TWAIN Local ty= field</param>
        /// <param name="a_szTwainLocalSerialNumber">TWAIN serial number (from CAP_SERIALNUMBER)</param>
        /// <param name="szTwainLocalNote">TWAIN Local note= field</param>
        /// <param name="a_szScanner">the complete scanner record</param>
        /// <param name="isDatTwainDirectSupported">the register.txt stuff</param>
        public void Set
        (
            string a_szTwainLocalTy,
            string a_szTwainLocalSerialNumber,
            string a_szTwainLocalNote,
            string a_szScanner,
            string a_szTwainLocalScanner = null
        )
        {
            // Init stuff...
            m_device = default(Device);

            // If we don't have a valid ty, then scoot...
            if (string.IsNullOrEmpty(a_szTwainLocalTy))
            {
                Log.Error("a_szTwainLocalTy is empty...");
                return;
            }

            // Stock the new device...
            m_device.szTwainLocalTy = a_szTwainLocalTy;
            m_device.szTwainLocalSerialNumber = a_szTwainLocalSerialNumber;
            m_device.szTwainLocalNote = a_szTwainLocalNote;
            m_device.szScanner = a_szScanner;

            // If the note is empty, use the type...
            if (string.IsNullOrEmpty(m_device.szTwainLocalNote))
            {
                m_device.szTwainLocalNote = m_device.szTwainLocalTy;
            }

            // Fix the serial number, if we didn't get one...
            if (string.IsNullOrEmpty(m_device.szTwainLocalSerialNumber))
            {
                m_device.szTwainLocalInstanceName = Dns.GetHostName();
            }

            // Build the instance name...
            int ii;
            m_device.szTwainLocalInstanceName = m_device.szTwainLocalTy + "_" + m_device.szTwainLocalSerialNumber;
            for (ii = 0; ii < m_device.szTwainLocalInstanceName.Length; ii++)
            {
                // Replace anything that's not A-Z,a-z,0-9 or _ with an _
                if (!char.IsLetterOrDigit(m_device.szTwainLocalInstanceName[ii]) && (m_device.szTwainLocalInstanceName[ii] != '_'))
                {
                    m_device.szTwainLocalInstanceName = m_device.szTwainLocalInstanceName.Remove(ii, 1).Insert(ii, "_");
                }
            }

            // Load the inquiry data...
            if (!string.IsNullOrEmpty(a_szTwainLocalScanner))
            {
                m_device.twaininquirydata = TwainInquiryData.Deserialize(a_szTwainLocalScanner);
            }

            // If its null, then make one...
            else if (m_device.twaininquirydata == null)
            {
                m_device.twaininquirydata = new TwainInquiryData();
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// Report the level of TWAIN Direct support.  The order
        /// matters, and must go from least support to most support...
        /// 
        /// None     - the driver is not safe for use.
        /// Basic    - requires TWAIN Bridge to handle TWAIN Direct
        ///            features, but didn't answer all inquires in
        ///            a way that inspires full confidence, so this
        ///            will do basics: flatbed, feeder, imageFormat,
        ///            compression, resolution, and probably some
        ///            kind of cropping support.
        /// Extended - requires TWAIN Bridge to handle TWAIN Direct
        ///            features, but can safely use more advanced
        ///            capabilities, like feederFront, feederRear,
        ///            automatic color detection, blank image removal,
        ///            and such.
        /// Driver   - the TWAIN driver can handle TWAIN Direct tasks
        ///            and return metadata and PDF/raster images.
        /// </summary>
        public enum TwainDirectSupport
        {
            Undefined = 0,
            None = 1,
            Basic = 2,
            Extended = 3,
            Driver = 4
        }

        /// <summary>
        /// Information collected as part of TwainInquiry...
        /// </summary>
        public class TwainInquiryData
        {
            /// <summary>
            ///  Don't just stand there, construct something!
            /// </summary>
            public TwainInquiryData()
            {
                m_twaindirectsupport = TwainDirectSupport.Undefined;
            }

            /// <summary>
            /// Deserialize the JSON back into us...
            /// </summary>
            /// <returns>a JSON object</returns>
            public static TwainInquiryData Deserialize(string a_szJson)
            {
                bool blSuccess;
                long lJsonErrorIndex;
                string szValue;
                JsonLookup jsonlookup;
                TwainInquiryData twaininquirydata = new TwainInquiryData();

                // Nada...
                if (string.IsNullOrEmpty(a_szJson))
                {
                    return (null);
                }

                // Load the JSON...
                jsonlookup = new JsonLookup();
                blSuccess = jsonlookup.Load(a_szJson, out lJsonErrorIndex);
                if (!blSuccess)
                {
                    Log.Error("bad TwainInquiryData - <" + a_szJson + ">");
                    return (null);
                }

                // TW_IDENTITY.Manufacturer...
                twaininquirydata.m_szTwidentityManufacturer = jsonlookup.Get("twidentityManufacturer", false);

                // TW_IDENTITY.ProductName...
                twaininquirydata.m_szTwidentityProductName = jsonlookup.Get("twidentityProductName", false);

                // TW_IDENTITY.ProtocolVersion : TW_IDENTITY.Version...
                twaininquirydata.m_szTwidentityVersion = jsonlookup.Get("twidentityVersion", false);

                // TWAIN Direct Support...
                szValue = jsonlookup.Get("twainDirectSupport", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_twaindirectsupport = TwainDirectSupport.None; break;
                    case "none": twaininquirydata.m_twaindirectsupport = TwainDirectSupport.None; break;
                    case "basic": twaininquirydata.m_twaindirectsupport = TwainDirectSupport.Basic; break;
                    case "extended": twaininquirydata.m_twaindirectsupport = TwainDirectSupport.Extended; break;
                    case "driver": twaininquirydata.m_twaindirectsupport = TwainDirectSupport.Driver; break;
                }

                // CAP_AUTOMATICSENSEMEDIUM?
                szValue = jsonlookup.Get("isAutomaticSenseMedium", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blAutomaticSenseMedium = false; break;
                    case "false": twaininquirydata.m_blAutomaticSenseMedium = false; break;
                    case "true": twaininquirydata.m_blAutomaticSenseMedium = true; break;
                }

                // DAT_TWAINDIRECT?
                szValue = jsonlookup.Get("isDatTwainDirectSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blDatTwainDirect = false; break;
                    case "false": twaininquirydata.m_blDatTwainDirect = false; break;
                    case "true": twaininquirydata.m_blDatTwainDirect = true; break;
                }

                // Device online? (just keeping a record of what we saw at the time we captured this)
                szValue = jsonlookup.Get("isDeviceOnline", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blDeviceOnline = false; break;
                    case "false": twaininquirydata.m_blDeviceOnline = false; break;
                    case "true": twaininquirydata.m_blDeviceOnline = true; break;
                }

                // DAT_EXTIMAGEINFO?
                szValue = jsonlookup.Get("isExtImageInfoSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blExtImageInfo = false; break;
                    case "false": twaininquirydata.m_blExtImageInfo = false; break;
                    case "true": twaininquirydata.m_blExtImageInfo = true; break;
                }

                // CAP_FEEDERENABLED (true)?
                szValue = jsonlookup.Get("isFeederDetected", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blFeederDetected = false; break;
                    case "false": twaininquirydata.m_blFeederDetected = false; break;
                    case "true": twaininquirydata.m_blFeederDetected = true; break;
                }

                // CAP_FEEDERENABLED (false)?
                szValue = jsonlookup.Get("isFlatbedDetected", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blFlatbedDetected = false; break;
                    case "false": twaininquirydata.m_blFlatbedDetected = false; break;
                    case "true": twaininquirydata.m_blFlatbedDetected = true; break;
                }

                // DAT_IMAGEMEMFILEXFER?
                szValue = jsonlookup.Get("isImageMemFileXferSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blImageMemFileXfer = false; break;
                    case "false": twaininquirydata.m_blImageMemFileXfer = false; break;
                    case "true": twaininquirydata.m_blImageMemFileXfer = true; break;
                }

                // CAP_PAPERDETECTABLE?
                szValue = jsonlookup.Get("isPaperDetectableSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blPaperDetectable = false; break;
                    case "false": twaininquirydata.m_blPaperDetectable = false; break;
                    case "true": twaininquirydata.m_blPaperDetectable = true; break;
                }

                // TWFF_PDFRASTER?
                szValue = jsonlookup.Get("isPdfRasterSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blPdfRaster = false; break;
                    case "false": twaininquirydata.m_blPdfRaster = false; break;
                    case "true": twaininquirydata.m_blPdfRaster = true; break;
                }

                // DAT_PENDINGXFERS/MSG_RESET?
                szValue = jsonlookup.Get("isPendingXfersResetSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blPendingXfersReset = false; break;
                    case "false": twaininquirydata.m_blPendingXfersReset = false; break;
                    case "true": twaininquirydata.m_blPendingXfersReset = true; break;
                }

                // DAT_PENDINGXFERS/MSG_STOPFEEDER?
                szValue = jsonlookup.Get("isPendingXfersStopFeederSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blPendingXfersStopFeeder = false; break;
                    case "false": twaininquirydata.m_blPendingXfersStopFeeder = false; break;
                    case "true": twaininquirydata.m_blPendingXfersStopFeeder = true; break;
                }

                // CAP_SHEETCOUNT?
                szValue = jsonlookup.Get("isSheetCountSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blSheetCount = false; break;
                    case "false": twaininquirydata.m_blSheetCount = false; break;
                    case "true": twaininquirydata.m_blSheetCount = true; break;
                }

                // TWEI_TWAINDIRECTMETADATA?
                szValue = jsonlookup.Get("isTweiTwainDirectMetadataSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blTweiTwainDirectMetadata = false; break;
                    case "false": twaininquirydata.m_blTweiTwainDirectMetadata = false; break;
                    case "true": twaininquirydata.m_blTweiTwainDirectMetadata = true; break;
                }

                // CAP_UICONTROLLABLE?
                szValue = jsonlookup.Get("isUiControllableSupported", false);
                if (szValue == null) szValue = "";
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blUiControllable = false; break;
                    case "false": twaininquirydata.m_blUiControllable = false; break;
                    case "true": twaininquirydata.m_blUiControllable = true; break;
                }

                // Hostname...
                twaininquirydata.m_szHostname = jsonlookup.Get("hostName", false);

                // Serial number...
                twaininquirydata.m_szSerialnumber = jsonlookup.Get("serialNumber", false);

                // Serial number...
                szValue = jsonlookup.Get("numberOfSheets", false);
                switch (szValue.ToLowerInvariant())
                {
                    default: twaininquirydata.m_blSheetCount = false; break;
                    case "[1, 1]": twaininquirydata.m_blSheetCount = false; break;
                    case "[1, 32767]": twaininquirydata.m_blSheetCount = true; break;
                }

                // Resolutions...
                twaininquirydata.m_szResolutions = jsonlookup.Get("resolutions", false);

                // Height...
                twaininquirydata.m_szHeight = jsonlookup.Get("height", false);

                // Width...
                twaininquirydata.m_szWidth = jsonlookup.Get("width", false);

                // Offset-X...
                twaininquirydata.m_szOffsetX = jsonlookup.Get("offsetX", false);

                // Offset-Y...
                twaininquirydata.m_szOffsetY = jsonlookup.Get("offsetY", false);

                // Croppings...
                twaininquirydata.m_szCroppings = jsonlookup.Get("croppings", false);

                // Pixel formats...
                twaininquirydata.m_szPixelFormats = jsonlookup.Get("pixelFormats", false);

                // Camera sides...
                twaininquirydata.m_szCameraSides = jsonlookup.Get("cameraSides", false);

                // Compressions...
                twaininquirydata.m_szCompressions = jsonlookup.Get("compressions", false);

                // All done...
                return (twaininquirydata);
            }

            /// <summary>
            /// Get CAP_AUTOMATICSENSEMEDIUM...
            /// </summary>
            /// <returns>true if supported</returns>
            public bool GetAutomaticSenseMedium()
            {
                return (m_blAutomaticSenseMedium);
            }

            /// <summary>
            /// Get CAP_CAMERASIDE values...
            /// </summary>
            /// <returns>JSON array of camera side values</returns>
            public string GetCameraSides()
            {
                return (m_szCameraSides);
            }

            /// <summary>
            /// Get JSON array of compressions...
            /// </summary>
            /// <returns>JSON array of compressions</returns>
            public string GetCompressions()
            {
                return (m_szCompressions);
            }

            /// <summary>
            /// Get JSON array of cropping values...
            /// </summary>
            /// <returns>JSON array of cropping values</returns>
            public string GetCroppings()
            {
                return (m_szCroppings);
            }

            /// <summary>
            /// Get the DAT_TWAINDIRECT...
            /// </summary>
            /// <returns>true if DAT_TWAINDIRECT is supported</returns>
            public bool GetDatTwainDirect()
            {
                return (m_blDatTwainDirect);
            }

            /// <summary>
            /// Get the device online setting...
            /// </summary>
            /// <returns>true if the device is online</returns>
            public bool GetDeviceOnline()
            {
                return (m_blDeviceOnline);
            }

            /// <summary>
            /// Get the extended image info setting...
            /// </summary>
            /// <returns>true if extended image info is supported</returns>
            public bool GetExtImageInfo()
            {
                return (m_blExtImageInfo);
            }

            /// <summary>
            /// Get the feeder detected setting...
            /// </summary>
            /// <returns>true if we have a feeder</returns>
            public bool GetFeederDetected()
            {
                return (m_blFeederDetected);
            }

            /// <summary>
            /// Get the flatbed detected setting...
            /// </summary>
            /// <returns>true if we have a flatbed</returns>
            public bool GetFlatbedDetected()
            {
                return (m_blFlatbedDetected);
            }

            /// <summary>
            /// Get JSON min,max height
            /// </summary>
            /// <returns>[min,max]</returns>
            public string GetHeight()
            {
                return (m_szHeight);
            }

            /// <summary>
            /// Get the image mem file setting...
            /// </summary>
            /// <returns>true if support mem file transfers</returns>
            public bool GetImageMemFileXfer()
            {
                return (m_blImageMemFileXfer);
            }

            /// <summary>
            /// Get the TW_IDENTITY.Manufacturer
            /// </summary>
            /// <returns>manufacturer name</returns>
            public string GetManufacturer()
            {
                return (m_szTwidentityManufacturer);
            }

            /// <summary>
            /// Get JSON min,max offsetx
            /// </summary>
            /// <returns>[min,max]</returns>
            public string GetOffsetX()
            {
                return (m_szOffsetX);
            }

            /// <summary>
            /// Get JSON min,max offsety
            /// </summary>
            /// <returns>[min,max]</returns>
            public string GetOffsetY()
            {
                return (m_szOffsetY);
            }

            /// <summary>
            /// Get the paper detect setting...
            /// </summary>
            /// <returns>true if we can detect paper</returns>
            public bool GetPaperDetectable()
            {
                return (m_blPaperDetectable);
            }

            /// <summary>
            /// Get the PDF/raster setting...
            /// </summary>
            /// <returns>true if we support PDF/raster</returns>
            public bool GetPdfRaster()
            {
                return (m_blPdfRaster);
            }

            /// <summary>
            /// Get the reset setting...
            /// </summary>
            /// <returns>true if reset is supported</returns>
            public bool GetPendingXfersReset()
            {
                return (m_blPendingXfersReset);
            }

            /// <summary>
            /// Get the stop feeder setting...
            /// </summary>
            /// <returns>true if stop feeder is supported</returns>
            public bool GetPendingXfersStopFeeder()
            {
                return (m_blPendingXfersStopFeeder);
            }

            /// <summary>
            /// Get the JSON array of pixelFormats...
            /// </summary>
            /// <returns>JSON array of pixelFormats</returns>
            public string GetPixelFormats()
            {
                return (m_szPixelFormats);
            }

            /// <summary>
            /// Get the TW_IDENTITY.ProductName
            /// </summary>
            /// <returns>product name</returns>
            public string GetProductName()
            {
                return (m_szTwidentityProductName);
            }

            /// <summary>
            /// Get a JSON array of resolutions...
            /// </summary>
            /// <returns>string or empty string</returns>
            public string GetResolutions()
            {
                return (string.IsNullOrEmpty(m_szResolutions) ? "" : m_szResolutions);
            }

            /// <summary>
            /// Get the serial number setting...
            /// </summary>
            /// <returns>the serial number, if there is one</returns>
            public string GetSerialNumber()
            {
                return (string.IsNullOrEmpty(m_szSerialnumber) ? "" : m_szSerialnumber);
            }

            /// <summary>
            /// Do we support sheet count?
            /// </summary>
            /// <returns>true if sheet count is supported</returns>
            public bool GetSheetCount()
            {
                return (m_blSheetCount);
            }

            /// <summary>
            /// Get the twain direct support setting...
            /// </summary>
            /// <returns>the level of twain direct support</returns>
            public TwainDirectSupport GetTwainDirectSupport()
            {
                return (m_twaindirectsupport);
            }

            /// <summary>
            /// Get the twain direct metadata setting...
            /// </summary>
            /// <returns>true if we can get metadata</returns>
            public bool GetTweiTwainDirectMetadata()
            {
                return (m_blTweiTwainDirectMetadata);
            }

            /// <summary>
            /// Get the ui controllable setting...
            /// </summary>
            /// <returns>true if we can control the ui</returns>
            public bool GetUiControllable()
            {
                return (m_blUiControllable);
            }

            /// <summary>
            /// Get the TW_IDENTITY.ProtocolVersion : TW_IDENTITY.Version...
            /// </summary>
            /// <returns>protocol version : version</returns>
            public string GetVersion()
            {
                return (m_szTwidentityVersion);
            }

            /// <summary>
            /// Get JSON min,max width
            /// </summary>
            /// <returns>[min,max]</returns>
            public string GetWidth()
            {
                return (m_szWidth);
            }

            /// <summary>
            /// Serialize the data into JSON...
            /// </summary>
            /// <param name="a_szTwidentityManufacturer">manufacturer</param>
            /// <param name="a_szTwidentityProductName">unique product name</param>
            /// <param name="a_szTwidentityVersion">protocol version : version</param>
            /// <returns>>a JSON string</returns>
            /// <summary>
            public string Serialize
            (
                string a_szTwidentityManufacturer,
                string a_szTwidentityProductName,
                string a_szTwidentityVersion
            )
            {
                string szJson = "";
                m_szTwidentityManufacturer = a_szTwidentityManufacturer;
                m_szTwidentityProductName = a_szTwidentityProductName;
                m_szTwidentityVersion = a_szTwidentityVersion;
                m_szHostname = Dns.GetHostName();

                // Start object...
                szJson += "{";
                szJson += "\"twidentityManufacturer\":\"" + m_szTwidentityManufacturer + "\",";
                szJson += "\"twidentityProductName\":\"" + m_szTwidentityProductName + "\",";
                szJson += "\"twidentityVersion\":\"" + m_szTwidentityVersion + "\",";
                szJson += "\"twainDirectSupport\":\"" + m_twaindirectsupport + "\",";
                szJson += "\"isAutomaticSenseMedium\":" + m_blAutomaticSenseMedium.ToString().ToLowerInvariant() + ",";
                szJson += "\"isDatTwainDirectSupported\":" + m_blDatTwainDirect.ToString().ToLowerInvariant() + ",";
                szJson += "\"isDeviceOnline\":" + m_blDeviceOnline.ToString().ToLowerInvariant() + ",";
                szJson += "\"isExtImageInfoSupported\":" + m_blExtImageInfo.ToString().ToLowerInvariant() + ",";
                szJson += "\"isFeederDetected\":" + m_blFeederDetected.ToString().ToLowerInvariant() + ",";
                szJson += "\"isFlatbedDetected\":" + m_blFlatbedDetected.ToString().ToLowerInvariant() + ",";
                szJson += "\"isImageMemFileXferSupported\":" + m_blImageMemFileXfer.ToString().ToLowerInvariant() + ",";
                szJson += "\"isPaperDetectableSupported\":" + m_blPaperDetectable.ToString().ToLowerInvariant() + ",";
                szJson += "\"isPdfRasterSupported\":" + m_blPdfRaster.ToString().ToLowerInvariant() + ",";
                szJson += "\"isPendingXfersResetSupported\":" + m_blPendingXfersReset.ToString().ToLowerInvariant() + ",";
                szJson += "\"isPendingXfersStopFeederSupported\":" + m_blPendingXfersStopFeeder.ToString().ToLowerInvariant() + ",";
                szJson += "\"isSheetCountSupported\":" + m_blSheetCount.ToString().ToLowerInvariant() + ",";
                szJson += "\"isTweiTwainDirectMetadataSupported\":" + m_blTweiTwainDirectMetadata.ToString().ToLowerInvariant() + ",";
                szJson += "\"isUiControllableSupported\":" + m_blUiControllable.ToString().ToLowerInvariant() + ",";
                szJson += "\"hostName\":\"" + m_szHostname + "\",";
                szJson += "\"serialNumber\":\"" + m_szSerialnumber + "\",";
                szJson += "\"numberOfSheets\":" + (m_blSheetCount ? "[1, 32767]" : "[1, 1]") + ",";
                szJson += "\"resolutions\":" + m_szResolutions + ",";
                szJson += "\"height\":" + m_szHeight + ",";
                szJson += "\"width\":" + m_szWidth + ",";
                szJson += "\"offsetX\":" + m_szOffsetX + ",";
                szJson += "\"offsetY\":" + m_szOffsetY + ",";
                szJson += "\"croppings\":" + m_szCroppings + ",";
                szJson += "\"pixelFormats\":" + m_szPixelFormats + ",";
                szJson += "\"cameraSides\":" + m_szCameraSides + ",";
                szJson += "\"compressions\":" + m_szCompressions; // last item, so no comma separator...
                szJson += "}";

                // All done...
                return (szJson);
            }

            /// <summary>
            /// Set CAP_AUTOMATICSENSEMEDIUM...
            /// </summary>
            public void SetAutomaticSenseMedium(bool a_blAutomaticSenseMedium)
            {
                m_blAutomaticSenseMedium = a_blAutomaticSenseMedium;
            }

            /// <summary>
            /// Set CAP_CAMERASIDE...
            /// </summary>
            public void SetCameraSides(string a_szCameraSides)
            {
                m_szCameraSides = a_szCameraSides;
            }

            /// <summary>
            /// Set JSON array of compressions...
            /// </summary>
            public void SetCompressions(string a_szCompressions)
            {
                m_szCompressions = a_szCompressions;
            }

            /// <summary>
            /// Set JSON array of cropping values...
            /// </summary>
            public void SetCroppings(string a_szCroppings)
            {
                m_szCroppings = a_szCroppings;
            }

            /// <summary>
            /// Set DAT_TWAINDIRECT...
            /// </summary>
            public void SetDatTwainDirect(bool a_blDatTwainDirect)
            {
                m_blDatTwainDirect = a_blDatTwainDirect;
            }

            /// <summary>
            /// Set device online...
            /// </summary>
            public void SetDeviceOnline(bool a_blDeviceOnline)
            {
                m_blDeviceOnline = a_blDeviceOnline;
            }

            /// <summary>
            /// Set extended image info...
            /// </summary>
            public void SetExtImageInfo(bool a_blExtImageInfo)
            {
                m_blExtImageInfo = a_blExtImageInfo;
            }

            /// <summary>
            /// Set feeder detected...
            /// </summary>
            public void SetFeederDetected(bool a_blFeederDetected)
            {
                m_blFeederDetected = a_blFeederDetected;
            }

            /// <summary>
            /// Set flatbed detected...
            /// </summary>
            public void SetFlatbedDetected(bool a_blFlatbedDetected)
            {
                m_blFlatbedDetected = a_blFlatbedDetected;
            }

            /// <summary>
            /// Set JSON min,max height
            /// </summary>
            public void SetHeight(string a_szHeight)
            {
                m_szHeight = a_szHeight;
            }

            /// <summary>
            /// Set image mem file...
            /// </summary>
            public void SetImageMemFileXfer(bool a_blImageMemFileXfer)
            {
                m_blImageMemFileXfer = a_blImageMemFileXfer;
            }

            /// <summary>
            /// Set JSON min,max offsetx
            /// </summary>
            public void SetOffsetX(string a_szOffsetX)
            {
                m_szOffsetX = a_szOffsetX;
            }

            /// <summary>
            /// Set JSON min,max offsety
            /// </summary>
            public void SetOffsetY(string a_szOffsetY)
            {
                m_szOffsetY = a_szOffsetY;
            }

            /// <summary>
            /// Set paper detectable...
            /// </summary>
            public void SetPaperDetectable(bool a_blPaperDetectable)
            {
                m_blPaperDetectable = a_blPaperDetectable;
            }

            /// <summary>
            /// Set PDF/raster...
            /// </summary>
            public void SetPdfRaster(bool a_blPdfRaster)
            {
                m_blPdfRaster = a_blPdfRaster;
            }

            /// <summary>
            /// Set reset...
            /// </summary>
            public void SetPendingXfersReset(bool a_blPendingXfersReset)
            {
                m_blPendingXfersReset = a_blPendingXfersReset;
            }

            /// <summary>
            /// Set stop feeder...
            /// </summary>
            public void SetPendingXfersStopFeeder(bool a_blPendingXfersStopFeeder)
            {
                m_blPendingXfersStopFeeder = a_blPendingXfersStopFeeder;
            }

            /// <summary>
            /// Set the JSON array of pixelFormats...
            /// </summary>
            public void SetPixelFormats(string a_szPixelFormats)
            {
                m_szPixelFormats = a_szPixelFormats;
            }

            /// <summary>
            /// Set a JSON array of resolutions...
            /// </summary>
            public void SetResolutions(string a_szResolutions)
            {
                m_szResolutions = a_szResolutions;
            }

            /// <summary>
            /// Set serial number...
            /// </summary>
            public void SetSerialNumber(string a_szSerialnumber)
            {
                m_szSerialnumber = a_szSerialnumber;
            }

            /// <summary>
            /// Set sheet count...
            /// </summary>
            public void SetSheetCount(bool a_blSheetCount)
            {
                m_blSheetCount = a_blSheetCount;
            }

            /// <summary>
            /// Set twain direct support...
            /// </summary>
            public void SetTwainDirectSupport(TwainDirectSupport a_twaindirectsupport)
            {
                m_twaindirectsupport = a_twaindirectsupport;
            }

            /// <summary>
            /// Set twain direct metadata...
            /// </summary>
            public void SetTweiTwainDirectMetadata(bool a_blTweiTwainDirectMetadata)
            {
                m_blTweiTwainDirectMetadata = a_blTweiTwainDirectMetadata;
            }

            /// <summary>
            /// Set ui controllable...
            /// </summary>
            public void SetUiControllable(bool a_blUiControllable)
            {
                m_blUiControllable = a_blUiControllable;
            }

            /// <summary>
            /// Set JSON min,max width
            /// </summary>
            public void SetWidth(string a_szWidth)
            {
                m_szWidth = a_szWidth;
            }

            /// <summary>
            /// Is CAP_AUTOMATICSENSEMEDIUM supported?
            /// </summary>
            /// <returns></returns>
            private bool m_blAutomaticSenseMedium;

            /// <summary>
            /// JSON array of camerasides, or an empty string...
            /// </summary>
            private string m_szCameraSides;

            /// <summary>
            /// JSON array of compressions...
            /// </summary>
            private string m_szCompressions;

            /// <summary>
            /// JSON array of cropping values...
            /// </summary>
            private string m_szCroppings;

            /// <summary>
            /// Is DAT_TWAINDIRECT supported?
            /// </summary>
            private bool m_blDatTwainDirect;

            /// <summary>
            /// Is the device online?
            /// </summary>
            private bool m_blDeviceOnline;

            /// <summary>
            /// Do we support DAT_EXTIMAGEINFO?
            /// </summary>
            private bool m_blExtImageInfo;

            /// <summary>
            /// Do we have a feeder?
            /// </summary>
            private bool m_blFeederDetected;

            /// <summary>
            /// Do we have a flatbed?
            /// </summary>
            private bool m_blFlatbedDetected;

            /// <summary>
            /// JSON array of min,max height...
            /// </summary>
            private string m_szHeight;

            /// <summary>
            /// The hostname for this PC...
            /// </summary>
            private string m_szHostname;

            /// <summary>
            /// Can we transfer memory files?
            /// </summary>
            private bool m_blImageMemFileXfer;

            /// <summary>
            /// JSON array of min,max offsetx...
            /// </summary>
            private string m_szOffsetX;

            /// <summary>
            /// JSON array of min,max offsety...
            /// </summary>
            private string m_szOffsetY;

            /// <summary>
            /// Can we detect the presence of paper?
            /// </summary>
            private bool m_blPaperDetectable;

            /// <summary>
            /// Do we support PDF/raster?
            /// </summary>
            private bool m_blPdfRaster;

            /// <summary>
            /// Do we support reset?
            /// </summary>
            private bool m_blPendingXfersReset;

            /// <summary>
            /// Do we support stopping the feeder?
            /// </summary>
            private bool m_blPendingXfersStopFeeder;

            /// <summary>
            /// JSON array of pixelFormats...
            /// </summary>
            private string m_szPixelFormats;

            /// <summary>
            /// A JSON array of resolutions...
            /// </summary>
            private string m_szResolutions;

            /// <summary>
            /// The serial number for this scanner...
            /// </summary>
            private string m_szSerialnumber;

            /// <summary>
            /// Is sheet count supported?
            /// </summary>
            private bool m_blSheetCount;

            /// <summary>
            /// What level of support did we come up with?
            /// </summary>
            private TwainDirectSupport m_twaindirectsupport;

            /// <summary>
            /// Is TWEI_TWAINDIRECTMETADATA supported?
            /// </summary>
            private bool m_blTweiTwainDirectMetadata;

            /// <summary>
            /// The TW_IDENTITY.Manufacturer of our scanner...
            /// </summary>
            private string m_szTwidentityManufacturer;

            /// <summary>
            /// The TW_IDENTITY.ProductName of our scanner...
            /// </summary>
            private string m_szTwidentityProductName;

            /// <summary>
            /// The TW_IDENTITY.ProtocolVersion : TW_IDENTITY.Version of our scanner...
            /// </summary>
            private string m_szTwidentityVersion;

            /// <summary>
            /// Can we control the UI?
            /// </summary>
            private bool m_blUiControllable;

            /// <summary>
            /// JSON array of min,max width...
            /// </summary>
            private string m_szWidth;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// Data about a single device...
        /// </summary>
        private struct Device
        {
            /// <summary>
            /// TWAIN Local user readable friendly name for the scanner, we're
            /// basing this on TWAIN's TW_IDENTITY-ProductName, which is what
            /// a vendor offers to a user to pick their scanner inside of the
            /// selection box...
            /// </summary>
            public string szTwainLocalTy;

            /// <summary>
            /// Serial number from the TWAIN Driver, obtained from the
            /// CAP_SERIALNUMBER capability.  If the user insists on using a
            /// scanner that doesn't have this value, we'll substitute it with
            /// the hostname...
            /// </summary>
            public string szTwainLocalSerialNumber;

            /// <summary>
            /// This is the combination of szTwainLocalTy + _ + szTwainLocalSerialNumber,
            /// which is used as the unique identifier on the full service name.  It
            /// has a limited character set, so anything that isn't A-Z,a-z,0-9 or _ is
            /// turned into a _.  You'll see this pop up in the mDNS in the form:
            /// ty_sn._twaindirect._sub._privet._tcp
            /// </summary>
            public string szTwainLocalInstanceName;

            /// <summary>
            /// TWAIN Local note, this is an optional string provided by the
            /// user to identify their scanner.  As such we'll offer it as the
            /// primary identifier.  However, since it's not guaranteed to be
            /// unique, we'll show the szTwainLocalTy, the szTwainLocalSerialNumber
            /// and the IP address as well, which in combination is guaranteeed
            /// to be unique on the local area network...
            /// </summary>
            public string szTwainLocalNote;

            /// <summary>
            /// The complete scanner record collected by TwainInquiry().  Everything
            /// TwainDirectOnTwain can tell us about this device that might make it
            /// easier to support.
            /// </summary>
            public string szScanner;

            /// <summary>
            /// The data in easily chewable form...
            /// </summary>
            public TwainInquiryData twaininquirydata;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// Our device data (some of this is persistent)...
        /// </summary>
        private Device m_device;

        #endregion
    }
}
