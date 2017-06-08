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
//  Copyright (C) 2014-2017 Kodak Alaris Inc.
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
using System.IO;
using System.Net;
using System.Threading;

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
        /// Return the TWAIN ty= field...
        /// </summary>
        /// <returns>the access token</returns>
        public string GetTwainLocalTy()
        {
            return (m_device.szTwainLocalTy);
        }

        /// <summary>
        /// Return the TWAIN Local serial number...
        /// </summary>
        /// <returns>the serial number</returns>
        public string GetTwainLocalSerialNumber()
        {
            return (m_device.szTwainLocalSerialNumber);
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
        /// Load data from a file...
        /// </summary>
        /// <param name="a_szFile">the file to load it from</param>
        /// <returns>try if successful</returns>
        public bool Load(string a_szFile)
        {
            try
            {
                // No file...
                if (!File.Exists(a_szFile))
                {
                    return (false);
                }

                // Parse it...
                long lResponseCharacterOffset;
                JsonLookup jsonlookup = new JsonLookup();
                jsonlookup.Load(File.ReadAllText(a_szFile), out lResponseCharacterOffset);

                // Start with a clean slate...
                m_device = default(Device);

                // Add the entry...
                Set
                (
                    jsonlookup.Get("scanner.twainLocalTy"),
                    jsonlookup.Get("scanner.twainLocalSerialNumber"),
                    jsonlookup.Get("scanner.twainLocalNote")
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
                szData += "{\n";

                // Scanner data...
                szData += "    \"scanner\": {\n";

                // Persist the items we want to remember for the user, technically we
                // shouldn't hold onto the serial number, because user's might move
                // scanners around, but it's so expensive to get the value (in terms
                // of performance) that we'l going to take the risk...
                szData += "        \"twainLocalTy\": \"" + m_device.szTwainLocalTy + "\",\n";
                szData += "        \"twainLocalSerialNumber\": \"" + m_device.szTwainLocalSerialNumber + "\",\n";
                szData += "        \"twainLocalNote\": \"" + m_device.szTwainLocalNote + "\"\n";

                // End of scanner object...
                szData += "    }\n";

                // End of root object...
                szData += "}\n";

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
        public void Set
        (
            string a_szTwainLocalTy,
            string a_szTwainLocalSerialNumber,
            string a_szTwainLocalNote
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
