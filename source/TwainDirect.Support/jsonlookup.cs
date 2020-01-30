///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirect.Support.JsonLookup
//
// A single pass JSON parser with four important characteristics.
//
// - It doesn't require a strong binding to a class, so the incoming content can
//   be completely unpredictable.
//
// - It offers a convenient key lookup scheme, so that the caller doesn't have
//   to traverse the tree to get at a given element; it also supports returning
//   complex elements as strings, like arrays and objects.
//
// - It's written in such a way the transcribing it to C/C++ will be relatively
//   simple.
//
// - It's reasonably memory light, in that it creates a lookup list into the
//   original JSON string.
//
// The TWAIN Direct certification tests exercise this class for correctness.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Comment
//  M.McLaughlin    01-May-2014     Initial Version
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
using System.Collections.Generic;

namespace TwainDirect.Support
{
    /// <summary>
    /// This is a simple JSON parser.  It was written by staring at the JSON
    /// railroad diagrams and coding like a loon, and then stress testing it
    /// with a variety of real world strings.
    /// 
    /// It makes no claims to being overly fast.  It is relatively efficient
    /// insofar as it makes a single pass, doesn't munge the original string
    /// and creates a tree representing the data.
    /// 
    /// It's main benefit is that it doesn't require a binding to a strongly
    /// typed class, so the data can be literally anything.  And it supports
    /// a simple dotted notation for looking up content.  Adding functions
    /// to allow for enumerating the data wouldn't be hard, but haven't been
    /// added at this time.
    /// 
    /// Two functions do all the heavy lifting:
    /// 
    ///     bool Load(string a_szJson, out lResponseCharacterOffset)
    ///     Causes the class to parse the JSON data and create an internal
    ///     data structure for it.  If a problem occurs it returns false and
    ///     an index into the string showering where the badness occurred.
    ///     
    ///     string Get(string a_szKey)
    ///     Returns a string for the item that was found.  If you need to
    ///     know the type, then use the GetCheck function.
    ///     
    ///     Here's some sample JSON:
    ///         {
    ///             "array": [
    ///                 {
    ///                     "first": 1
    ///                 },
    ///                 {
    ///                     "second": {
    ///                         "third": 3
    ///                     }
    ///                 }
    ///             ]
    ///         }
    ///         
    ///     To get the data for "third" we use the key:
    ///     
    ///         array[1].second.third
    ///         
    ///     As you might expect, if the property name has a dot or square
    ///     brackets in it, that would cause a problem.  This isn't an issue
    ///     for how we plan to use it, but it would be possible to get
    ///     around it if needed, by using a prefix delimiter in the lookup
    ///     string.
    /// 
    /// Clarity is a good thing, and hopefully this code is fairly simple to
    /// chug through.
    /// </summary>
    public sealed class JsonLookup
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods...

        /// <summary>
        /// Init stuff...
        /// </summary>
        public JsonLookup()
        {
            m_blStrictParsingRules = false;
            m_property = null;
            m_szJson = null;
        }

        /// <summary>
        /// Dump the contents of the property tree...
        /// </summary>
        /// <returns>the JSON string</returns>
		public string Dump()
        {
            if (m_lkeyvalueOverride == null)
            {
                m_lkeyvalueOverride = new List<KeyValue>();
            }
            return (DumpPrivate(m_property, 0, "", false));
        }

        /// <summary>
        /// Does a find, and if the item is found, does a get...
        /// </summary>
        /// <param name="a_szKey">key to find</param>
        /// <param name="a_iStartingIndex">array index to start at</param>
        /// <param name="a_iCount">count of array elements to search</param>
        /// <param name="a_szValue">value to return</param>
        /// <param name="a_epropertytype">type of property that we've found</param>
        /// <param name="a_blLog">log if key isn't found</param>
        /// <returns>0 - n on success, -1 on failure</returns>
        public int FindGet(string a_szKey, int a_iStartingIndex, int a_iCount, out string a_szValue, out EPROPERTYTYPE a_epropertytype, bool a_blLog)
        {
	        bool blSuccess;
	        int iIndex;
            int iIndexBrackets;
	        string szKey;

            // Init stuff...
            a_szValue = "";
            a_epropertytype = EPROPERTYTYPE.UNDEFINED;

            // Find the item...
            iIndex = FindKey(a_szKey, a_iStartingIndex, a_iCount);
	        if (iIndex < 0)
	        {
		        return (iIndex);
	        }

            // If we found it, then create the full key at this index...
            iIndexBrackets = a_szKey.IndexOf("[].");
            if (iIndexBrackets < 0)
            {
                return (-1);
            }
            szKey = a_szKey.Substring(0, iIndexBrackets) + "[" + iIndex + "]." + a_szKey.Substring(iIndexBrackets + 3);

	        // And get the data...
	        blSuccess = GetCheck(szKey, out a_szValue, out a_epropertytype, a_blLog);
	        if (!blSuccess)
	        {
		        return (-1);
	        }

	        // All done...
	        return (iIndex);
        }

        /// <summary>
        ///	Find a key in an array of objects.  The JSON needs to take
        ///	a form similar to:
        ///		{ "array":[
        ///			{"key1":data},
        ///			{"key2":data},
        ///			{"key3":data},
        ///			...
        ///		}
        ///	In this case a search string could be:
        ///		array[].key2
        ///	Where [] indicates the array to be enumerated, and key2 is
        ///	the property name we're searching for.  In the case of a
        ///	rooted array like:
        ///		[
        ///			{"key1":data},
        ///			{"key2":data},
        ///			{"key3":data},
        ///			...
        ///		]
        ///	Then the search string would be [].key2
        /// </summary>
        /// <param name="a_szKey">key to find</param>
        /// <param name="a_iStartingIndex">array index to start at</param>
        /// <param name="a_iCount">count of array elements to search</param>
        /// <returns>0 - n on success, -1 on failure</returns>
        public int FindKey(string a_szKey, int a_iStartingIndex = 0, int a_iCount = 0)
        {
	        bool blSuccess;
	        int iCount;
	        int iIndex;
	        string szKey;
            string szKeyLeft;
            string szKeyRight;
            string szValue;
            EPROPERTYTYPE epropertytype;

            // Validate...
            if (string.IsNullOrEmpty(a_szKey) || (a_iStartingIndex < 0) || (a_iCount < 0))
	        {
		        return (-1);
	        }

            // The function is useless unless it can enumerate through an array,
            // so just bail if the user didn't ask for this...
            iIndex = a_szKey.IndexOf("[].");
            if (iIndex == -1)
	        {
		        return (-1);
	        }

            // Get the left and right sides of the key...
            szKeyLeft = a_szKey.Substring(0, iIndex);
            szKeyRight = a_szKey.Substring(iIndex + 3);

	        // The left side can be empty, but not the right side...
	        if (string.IsNullOrEmpty(szKeyRight))
	        {
		        return (-1);
	        }

	        // The left side must be found, and it must be an array...
	        blSuccess = GetCheck(szKeyLeft, out szValue, out epropertytype, false);
	        if (!blSuccess || (epropertytype != EPROPERTYTYPE.ARRAY))
	        {
		        return (-1);
	        }

	        // Figure out our endpoint in the array...
	        if (a_iCount == 0)
	        {
		        iCount = int.MaxValue;
	        }
	        else
	        {
		        iCount = a_iStartingIndex + a_iCount;
	        }

	        // Okay, it's time to loop.  We need to make sure the array item exists
	        // before we look for the key, so this is a two step process...
	        for (iIndex = a_iStartingIndex; iIndex < iCount; iIndex++)
	        {
                // Build the array key...
                szKey = szKeyLeft + "[" + iIndex + "]";

		        // Does the array element exist?  And is it an object?
		        blSuccess = GetCheck(szKey, out szValue, out epropertytype, false);
		        if (!blSuccess || (epropertytype != EPROPERTYTYPE.OBJECT))
		        {
			        return (-1);
		        }

                // Okay, try to get the full key...
                szKey += "." + szKeyRight;
		        blSuccess = GetCheck(szKey, out szValue, out epropertytype, false);
		        if (blSuccess)
		        {
			        return (iIndex);
		        }
	        }

	        // No joy...
	        return (-1);
        }

        /// <summary>
        /// Get the string associated with the key.  This is a convenience
        /// function, because in most cases getting back a null string is
        /// enough, so we don't need a special boolean check...
        /// </summary>
        /// <param name="a_szKey">dotted key notation</param>
        /// <param name="a_szValue">value to return</param>
        /// <returns>string on success, else null</returns>
        public string Get(string a_szKey, bool a_blLog = true)
        {
            bool blSuccess;
            string szValue;
            EPROPERTYTYPE epropertytype;

            // Do the lookup...
            blSuccess = GetCheck(a_szKey, out szValue, out epropertytype, a_blLog);

            // We're good...
            if (blSuccess)
            {
                return (szValue);
            }

            // Ruh-roh...
            return (null);
        }

        /// <summary>
        /// Get the string associated with the key, and let us know how
        /// the lookup turned out...
        /// </summary>
        /// <param name="a_szKey">dotted key notation</param>
        /// <param name="a_szValue">value to return</param>
        /// <param name="a_epropertytype">type of property that we've found</param>
        /// <param name="a_blLog">log if key isn't found</param>
        /// <returns>true on success</returns>
		public bool GetCheck(string a_szKey, out string a_szValue, out EPROPERTYTYPE a_epropertytype, bool a_blLog)
        {
	        string[] aszKey;
            string szIndex;
	        string szBaseName;
	        string szProperty;
	        UInt32 kk;
	        UInt32 uu;
	        UInt32 u32Index;
	        Property property;

            // Init...
            a_szValue = "";
            a_epropertytype = EPROPERTYTYPE.UNDEFINED;

	        // Validate...
	        if ((a_szKey == null) || (m_property == null))
	        {
                Log.Error("GetCheck: null argument...");
		        return (false);
	        }

	        // If the key is empty, return the whole object...
	        if (a_szKey.Length == 0)
	        {
		        a_szValue = m_szJson;
                a_epropertytype = m_property.epropertytype;
		        return (true);
	        }

	        // Fully tokenize the key so we can look ahead when needed...
            aszKey = a_szKey.Split('.');

	        // Search, always skip the root if it's an object,
            // if it's an array we need to process it...
            property = m_property;
            if (property.epropertytype == EPROPERTYTYPE.OBJECT)
            {
	        	property = m_property.propertyChild;
            }
	        for (kk = 0; kk < aszKey.Length; kk++)
	        {
		        // Extract the basename, in case we have an index...
                szBaseName = aszKey[kk];
                if (szBaseName.Contains("["))
                {
                    szBaseName = szBaseName.Substring(0,szBaseName.IndexOf('['));
                }

		        // Look for a match among the siblings at this level...
		        while (property != null)
		        {
			        GetProperty(property,out szProperty);
			        if (szProperty == szBaseName)
			        {
				        break;
			        }
			        property = property.propertySibling;
		        }

		        // No joy...
		        if (property == null)
		        {
                    if (a_blLog)
                    {
                        Log.Info("GetCheck: key not found..." + a_szKey);
                    }
			        return (false);
		        }

		        // If we found a value, then we're done...
		        if (	(property.epropertytype == EPROPERTYTYPE.STRING)
			        ||	(property.epropertytype == EPROPERTYTYPE.NUMBER)
			        ||	(property.epropertytype == EPROPERTYTYPE.BOOLEAN)
			        ||	(property.epropertytype == EPROPERTYTYPE.NULL))
		        {
			        // If there's more to the key, then we weren't successful...
			        if ((kk + 1) < aszKey.Length)
			        {
                        if (a_blLog)
                        {
                            Log.Info("GetCheck: key not found..." + a_szKey);
                        }
				        return (false);
			        }

			        // Return what we found...
                    return (GetValue(property, out a_szValue, out a_epropertytype));
		        }

		        // We found an object...
		        if (property.epropertytype == EPROPERTYTYPE.OBJECT)
		        {
			        // We've no more keys, so return the object...
			        if ((kk + 1) >= aszKey.Length)
			        {
                        return (GetValue(property, out a_szValue, out a_epropertytype));
			        }

			        // Otherwise, step into the object...
			        property = property.propertyChild;
			        continue;
		        }

		        // If we're an array, we need to walk the siblings of the child...
		        if (property.epropertytype == EPROPERTYTYPE.ARRAY)
		        {
			        // If we don't have a '[' and this is the last key, return the whole thing...
			        if (!aszKey[kk].Contains("[") && ((kk + 1) >= aszKey.Length))
			        {
                        return (GetValue(property, out a_szValue, out a_epropertytype));
			        }
			        // If we don't have a '[' and there is more to the key, we're in trouble...
			        else if (!aszKey[kk].Contains("[") && ((kk + 1) < aszKey.Length))
			        {
                        if (a_blLog)
                        {
                            Log.Info("GetCheck: key not found..." + a_szKey);
                        }
				        return (false);
			        }

			        // We must have a valid index in the key...
			        szIndex = aszKey[kk].Substring(aszKey[kk].IndexOf('['));
			        if ((szIndex.Length < 3) || !szIndex.StartsWith("[") || !szIndex.EndsWith("]"))
			        {
                        if (a_blLog)
                        {
                            Log.Info("GetCheck: key not found..." + a_szKey);
                        }
				        return (false);
			        }

			        // Get the basename and look for a match...
			        if (!UInt32.TryParse(szIndex.Substring(1,szIndex.Length - 2), out u32Index))
                    {
                        if (a_blLog)
                        {
                            Log.Info("GetCheck: key not found..." + a_szKey);
                        }
                        return (false);
                    }

			        // Step into the child...
			        property = property.propertyChild;
			        if (property == null)
			        {
                        if (a_blLog)
                        {
                            Log.Info("GetCheck: key not found..." + a_szKey);
                        }
				        return (false);
			        }

			        // Walk the siblings in this child...
			        for (uu = 0; uu < u32Index; uu++)
			        {
				        property = property.propertySibling;
				        if (property == null)
				        {
                            if (a_blLog)
                            {
                                Log.Info("GetCheck: key not found..." + a_szKey);
                            }
					        return (false);
				        }
			        }

			        // We've no more keys, so return the object...
			        if ((kk + 1) >= aszKey.Length)
			        {
                        return (GetValue(property, out a_szValue, out a_epropertytype));
			        }

			        // If the thing we hit is an object, then we need to step into it...
			        if (property.epropertytype == EPROPERTYTYPE.OBJECT)
			        {
				        property = property.propertyChild;
			        }

			        // Otherwise, keep on looking...
			        continue;
		        }

		        // Well, this was unexpected...
                Log.Info("GetCheck: unexpected error..." + a_szKey);
		        return (false);
	        }

	        // All done...
	        return (true);
        }

        /// <summary>
        /// Get the string associated with the key.  This is a convenience
        /// function, because in most cases getting back a null string is
        /// enough, so we don't need a special boolean check.
        /// 
        /// The caller of this function is expecting to get back JSON data
        /// in string form.  We examine that data, looking for evidence
        /// that the strings are escaped, and if so, we unescape them.
        /// </summary>
        /// <param name="a_szKey">dotted key notation</param>
        /// <param name="a_szValue">value to return</param>
        /// <returns>string on success, else null</returns>
        public string GetJson(string a_szKey)
        {
            bool blSuccess;
            int iIndex;
            string szValue;
            EPROPERTYTYPE epropertytype;

            // Do the lookup...
            blSuccess = GetCheck(a_szKey, out szValue, out epropertytype, true);

            // No joy...
            if (!blSuccess)
            {
                return (null);
            }

            // Look for the first double-quote in the string, if the character
            // before it is a backslash, then we'll replace all of the \" with
            // ".  By definition the " has to be in index 1 or higher for this
            // to make sense, so we can ignore index 0...
            iIndex = szValue.IndexOf('"');
            if (iIndex >= 1)
            {
                if (szValue[iIndex - 1] == '\\')
                {
                    szValue = szValue.Replace("\\\"", "\"");
                }
            }

            // Ruh-roh...
            return (szValue);
        }

        /// <summary>
        /// Get the property of the key, if not found we'll come back
        /// with undefined...
        /// </summary>
        /// <param name="a_szKey">dotted key notation</param>
        /// <returns>type of the property</returns>
        public JsonLookup.EPROPERTYTYPE GetType(string a_szKey)
        {
            bool blSuccess;
            string szValue;
            JsonLookup.EPROPERTYTYPE epropertytype;

            // Do the lookup...
            blSuccess = GetCheck(a_szKey, out szValue, out epropertytype, true);

            // We're good...
            if (blSuccess)
            {
                return (epropertytype);
            }

            // Ruh-roh...
            return (JsonLookup.EPROPERTYTYPE.UNDEFINED);
        }

        /// <summary>
        /// Get the JSON data as XML.  This is a simple name/value conversion...
        /// </summary>
        /// <param name="a_szRootName">instead of o, use this as the name for the outermost tag</o></param>
        /// <returns>the XML string</returns>
        public string GetXml(string a_szRootName = "")
        {
            return (GetXmlPrivate(m_property, a_szRootName, 0, ""));
        }

        /// <summary>
        /// Loads a JSON string...
        /// </summary>
        /// <param name="a_szJson">JSON string to parse</param>
        /// <param name="a_lJsonErrorindex">index where error occurred, if return is false</param>
        /// <returns>true on success</returns>
		public bool Load(string a_szJson, out long a_lJsonErrorindex)
        {
	        bool blSuccess;
	        UInt32 u32Json;

            // Init stuff...
            a_lJsonErrorindex = 0;

	        // Free old content...
	        Unload();

	        // We have no new data...
	        if (a_szJson == null)
	        {
		        return (true);
	        }

	        // Make a copy of the string, in C# we'll work with the
            // index instead of pointers like we do in C/C++...
            u32Json = 0;
	        m_szJson = a_szJson;

	        // Parse the JSON and return...
	        blSuccess = Deserialize(ref u32Json);
	        if (!blSuccess)
	        {
                a_lJsonErrorindex = u32Json;
		        Unload();
	        }

	         // All done...
	        return (blSuccess);
        }

        /// <summary>
        /// Add an override for the Dump.  A value of null can be used
        /// to "delete" an override.  Overrides are only supported for
        /// boolean, null, number, and string.  It would be possible to
        /// add support for array and object, but that seems a lot more
        /// risky, so I'm holding off for now.
        /// </summary>
        /// <param name="a_szKey"></param>
        /// <param name="a_szValue"></param>
        public void Override(string a_szKey, string a_szValue)
        {
            // Make sure we have our list...
            if (m_lkeyvalueOverride == null)
            {
                m_lkeyvalueOverride = new List<KeyValue>();
            }

            // If we already have this key, remove it...
            int iIndex = m_lkeyvalueOverride.FindIndex(item => item.szKey == a_szKey);
            if (iIndex >= 0)
            {
                m_lkeyvalueOverride.RemoveAt(iIndex);
            }

            // Add the new data to our list...
            if (a_szValue != null)
            {
                KeyValue keyvalue = new KeyValue();
                keyvalue.szKey = a_szKey;
                keyvalue.szValue = a_szValue;
                m_lkeyvalueOverride.Add(keyvalue);
            }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions...

        /// <summary>
        /// The kinds of data types we can get from the class...
        /// </summary>
        public enum EPROPERTYTYPE
        {
	        UNDEFINED	= 0,
	        ARRAY		= 1,
	        OBJECT		= 2,
	        STRING		= 3,
	        BOOLEAN	    = 4,
	        NUMBER		= 5,
	        NULL		= 6,
	        LAST
        };

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods...

        /// <summary>
        /// The control function for parsing the JSON string...
        /// </summary>
        /// <param name="a_u32Json">JSON to parse</param>
        /// <returns>true on success</returns>
        private bool Deserialize(ref UInt32 a_u32Json)
        {
	        UInt32 u32Json = a_u32Json;

	        // Validate...
	        if (string.IsNullOrEmpty(m_szJson))
	        {
                Log.Error("Deserialize: null arguments...");
		        return (false);
	        }

	        // Initialize the first property as root...
	        m_property = new Property();

	        // Clear any whitespace...
            if (!SkipWhitespace(ref u32Json))
            {
                Log.Error("Deserialize: we ran out of data...");
                a_u32Json = u32Json;
                return (false);
            }

	        // We have an object...
	        if (m_szJson[(int)u32Json] == '{')
	        {
		        // What we are...
		        m_property.epropertytype = EPROPERTYTYPE.OBJECT;

		        // We don't need a colon, we just go straight to looking for the object,
		        // this function returns the closing curly bracket (if it finds it)...
		        if (!ParseObject(m_property, ref u32Json))
		        {
                    Log.Error("Deserialize: ParseObject failed...");
                    a_u32Json = u32Json;
			        return (false);
		        }
	        }

	        // Else we have an array...
	        else if (m_szJson[(int)u32Json] == '[')
	        {
		        // What we are...
		        m_property.epropertytype = EPROPERTYTYPE.ARRAY;

		        // We don't need a colon, we just go straight to looking for the object,
		        // this function returns the closing curly bracket (if it finds it)...
		        if (!ParseArray(m_property, ref u32Json))
		        {
                    Log.Error("Deserialize: ParseArray failed...");
                    a_u32Json = u32Json;
			        return (false);
		        }
	        }

	        // Else we have a problem...
	        else
	        {
                Log.Error("Deserialize: bad token...");
		        a_u32Json = u32Json;
		        return (false);
	        }


	        // All of the remaining content can only be whitespace...
	        if (SkipWhitespace(ref u32Json))
	        {
                Log.Error("Deserialize: found cruft...");
		        a_u32Json = u32Json;
		        return (false);
	        }

	        // All done...
	        a_u32Json = u32Json;
	        return (true);
        }

        /// <summary>
        /// Diagnostic dump of the results of a Load, this function
        /// runs recursively...
        /// </summary>
        /// <param name="a_property">property to dump</param>
        /// <param name="a_iDepth">depth we're at</param>
        /// <param name="a_szKey">key for this item</param>
        /// <param name="a_blArray">true if we're elements in an array</param>
        /// <returns>the JSON string</returns>
        private string DumpPrivate
        (
            Property a_property,
            int a_iDepth,
            string a_szKey,
            bool a_blArray
        )
        {
            int iArray;
            string szKey;
	        string szName;
            string szValue;
            string szResult;
	        Property property;
            KeyValue keyvalue;
            EPROPERTYTYPE epropertytype;

            // Init...
            iArray = -1;
            szResult = "";
            property = a_property;
	        if (property == null)
	        {
		        property = m_property;
	        }

	        // Dump...
	        while (property != null)
	        {
                // Our key...
                szKey = a_szKey;

                // We're in an array, so subscript us...
                if (a_blArray)
                {
                    iArray += 1;
                    szKey += "[" + iArray + "]";
                }

                switch (property.epropertytype)
                {
                    // This can't be right...
                    default:
                        return ("");

                    // Dump an array...
                    case EPROPERTYTYPE.ARRAY:
                        // name:[ or just [
                        GetProperty(property, out szName);
                        if (!string.IsNullOrEmpty(szName))
                        {
                            szResult += "\"" + szName + "\":[";
                            szKey += string.IsNullOrEmpty(szKey) ? szName : "." + szName;
                        }
                        else
                        {
                            szResult += "[";
                        }

                        // If we have a kiddie, dive down into it...
                        if (property.propertyChild != null)
                        {
                            szResult += DumpPrivate(property.propertyChild, a_iDepth + 1, szKey, true);
                        }

                        // If the last character is a comma, remove it...
                        if (szResult.EndsWith(","))
                        {
                            szResult = szResult.Remove(szResult.Length - 1);
                        }

                        // just ],
                        szResult += "],";
                        break;

                    // Dump a boolean, null, or number...
                    case EPROPERTYTYPE.BOOLEAN:
                    case EPROPERTYTYPE.NULL:
                    case EPROPERTYTYPE.NUMBER:
                        GetProperty(property, out szName);
                        szKey += string.IsNullOrEmpty(szKey) ? szName : "." + szName;
                        keyvalue = m_lkeyvalueOverride.Find(item => item.szKey == szKey);
                        if ((keyvalue == null) || (keyvalue.szValue == null))
                        {
                            GetValue(property, out szValue, out epropertytype);
                            szResult += "\"" + szName + "\":" + szValue + ",";
                        }
                        else
                        {
                            szResult += "\"" + szName + "\":" + keyvalue.szValue + ",";
                        }
                        break;

                    // Dump an object...
                    case EPROPERTYTYPE.OBJECT:
                        // name:{ or just {
                        GetProperty(property, out szName);
                        if (!string.IsNullOrEmpty(szName))
                        {
                            szResult += "\"" + szName + "\":{";
                            szKey += string.IsNullOrEmpty(szKey) ? szName : "." + szName;
                        }
                        else
                        {
                            szResult += "{";
                        }

                        // If we have a kiddie, dive down into it...
                        if (property.propertyChild != null)
                        {
                            szResult += DumpPrivate(property.propertyChild, a_iDepth + 1, szKey, false);
                        }

                        // If the last character is a comma, remove it...
                        if (szResult.EndsWith(","))
                        {
                            szResult = szResult.Remove(szResult.Length - 1);
                        }

                        // just },
                        szResult += "},";
                        break;

                    // Dump a string...
                    case EPROPERTYTYPE.STRING:
                        GetProperty(property, out szName);
                        szKey += string.IsNullOrEmpty(szKey) ? szName : "." + szName;
                        keyvalue = m_lkeyvalueOverride.Find(item => item.szKey == szKey);
                        if ((keyvalue == null) || (keyvalue.szValue == null))
                        {
                            GetValue(property, out szValue, out epropertytype);
                            szResult += "\"" + szName + "\":\"" + szValue + "\",";
                        }
                        else
                        {
                            szResult += "\"" + szName + "\":\"" + keyvalue.szValue + "\",";
                        }
                        break;
                }

		        // Next sibling...
		        property = property.propertySibling;
	        }

            // If the last character is a comma, remove it...
            if (szResult.EndsWith(","))
            {
                szResult = szResult.Remove(szResult.Length - 1);
            }

            // All done...
            return (szResult);
        }

        /// <summary>
        /// Emit the JSON data as compact XML.  We're doing this in a
        /// literal fashion.  Therefore:
        /// 
        ///    {
        ///        "metadata": {
        ///             "address": {
        ///	                "imageNumber": 1,
        ///	                "imagePart": 1,
        ///	                "imagePartNum": 1,
        ///	                "moreParts": lastPartInFile,
        ///	                "sheetNumber": 1,
        ///	                "source": "feederFront",
        ///	                "streamName": "stream0",
        ///	                "sourceName": "source0",
        ///	                "pixelFormatName": "pixelFormat0"
        ///             },
        ///             "image": {
        ///	                "compression": "none",
        ///	                "pixelFormat": "bw1",
        ///	                "pixelHeight": 2200,
        ///	                "pixelOffsetX": 0,
        ///	                "pixelOffsetY": 0,
        ///	                "pixelWidth": 1728,
        ///	                "resolution": 200,
        ///	                "size": 476279
        ///             },
		///             "status": {
		///	                 "success": true
		///             }
	    ///        }
        ///    }
        ///    
        /// Appears in XML as:
        ///    <o>
        ///        <o:metadata>
        ///             <o:address>
        ///	                <n:imageNumber>1</n:imageNumber>
        ///	                <n:imagePart>1</n:imagePart>
        ///	                <n:imagePartNum>1</n:imagePartNum>
        ///	                <s:moreParts>lastPartInFile</s:moreParts>
        ///	                <n:sheetNumber>1</n:sheetNumber>
        ///	                <s:source>feederFront</s:source>
        ///	                <s:streamName>stream0</s:streamName>
        ///	                <s:sourceName>source0</s:sourceName>
        ///	                <s:pixelFormatName>pixelFormat0</s:pixelFormatName>
        ///             </o:address>
        ///             <o:image>
        ///	                <s:compression>none</s:compression>
        ///	                <pixelFormat>bw1</n:pixelFormat>
        ///	                <n:pixelHeight>2200</n:pixelHeight>
        ///	                <n:pixelOffsetX>0</n:pixelOffsetX>
        ///	                <n:pixelOffsetY>0</n:pixelOffsetY>
        ///	                <n:pixelWidth>1728</n:pixelWidth>
        ///	                <n:resolution>200</n:resolution>
        ///	                <n:size>476279</n:size>
        ///             </o:image>
		///             <o:status>
		///	                 <s:success>true</s:success>
		///             </o:status>
	    ///        </o:metadata>
        ///    </o>
        ///    
        /// Arrays are handle like so:
        /// 
        ///     {
        ///         "array": [1, 2, 3]
        ///     }
        ///     
        ///     <o>
        ///         <a:array>
        ///             <n:item>1</n:item>
        ///             <n:item>2</n:item>
        ///             <n:item>3</n:item>
        ///         </a:array>
        ///     </o>
        ///     
        /// We do allow overriding the outermost tag, so that instead
        /// of "o" it can be something a little more descriptive, like
        /// "tdm" for TWAIN Direct metadata...
        /// 
        /// </summary>
        /// <param name="a_property">property to emit</param>
        /// <param name="a_szRootName">rootname to use for outermost tag at depth 0</param>
        /// <param name="a_iDepth">depth we're at</param>
        /// <param name="a_szXml">current string provided by caller</param>
        /// /// <returns>an XML string, or null on error</returns>
        private string GetXmlPrivate(Property a_property, string a_szRootName, int a_iDepth, string a_szXml)
        {
            string szXml = a_szXml;
            string szData;
            string szName;
            Property property;
            EPROPERTYTYPE epropertytype;

            // Init...
            property = a_property;
            if (property == null)
            {
                return (null);
            }

            // Loopy...
            while (property != null)
            {
                // Get the name...
                if (!GetProperty(property, out szName))
                {
                    return (null);
                }

                // If we didn't get a name, make one up...
                if (string.IsNullOrEmpty(szName))
                {
                    switch (property.epropertytype)
                    {
                        default: szName = "z"; break;
                        case EPROPERTYTYPE.ARRAY: szName = "a"; break;
                        case EPROPERTYTYPE.OBJECT:
                            // We can override the outermost tag...
                            if (!string.IsNullOrEmpty(a_szRootName) && (a_iDepth == 0))
                            {
                                szName = a_szRootName;
                            }
                            else
                            {
                                szName = "o";
                            }
                            break;
                    }
                }
                  
                // If we got a name, prefix it with obj or arr, if needed...
                else
                {
                    switch (property.epropertytype)
                    {
                        default: szName = "z:" + szName; break;
                        case EPROPERTYTYPE.ARRAY: szName = "a:" + szName; break;
                        case EPROPERTYTYPE.BOOLEAN: szName = "b:" + szName; break;
                        case EPROPERTYTYPE.NULL: szName = "u:" + szName; break;
                        case EPROPERTYTYPE.NUMBER: szName = "n:" + szName; break;
                        case EPROPERTYTYPE.OBJECT: szName = "o:" + szName; break;
                        case EPROPERTYTYPE.STRING: szName = "s:" + szName; break;
                    }
                }

                // ADD: our opening tag...
                szXml += "<" + szName + ">";

                // Get the value...
                if (!GetValue(property, out szData, out epropertytype))
                {
                    return (null);
                }

                // Dive into our kiddie, if we have one...
                if (property.propertyChild != null)
                {
                    // Dive in...
                    szXml = GetXmlPrivate(property.propertyChild, "", a_iDepth + 1, szXml);
                    if (szXml == null)
                    {
                        return (null);
                    }
                }
                else
                {
                    szXml += szData;
                }

                // ADD: our closing tag...
                szXml += "</" + szName + ">";

                // Next sibling...
                property = property.propertySibling;
            }

            // This is what we have so far...
            return (szXml);
        }

        /// <summary>
        /// Free a property tree...
        /// </summary>
        /// <param name="a_property"></param>
        private void FreeProperty(Property a_property)
        {
	        Property property;
	        Property propertySibling;

	        // Validate...
	        if (a_property == null)
	        {
		        return;
	        }

	        // Remove siblings, go after children as needed...
	        for (property = a_property; property != null; property = propertySibling)
	        {
		        // Next sibling...
		        propertySibling = property.propertySibling;

		        // Remove kiddies...
		        if (property.propertyChild != null)
		        {
			        FreeProperty(property.propertyChild);
			        property.propertyChild = null;
		        }

		        // Remove ourselves...
		        property = null;
	        }
        }

        /// <summary>
        /// Get a property name.  When the JSON rules are relaxed we allow
        /// for the following combinations:
        ///
        ///     "property": ...
        ///     'property': ...
        ///     \"property\": ...
        ///     \'property\': ...
        ///     property: ...
        ///     
        /// </summary>
        /// <param name="a_property">our place in the tree</param>
        /// <param name="a_szProperty">whatever we find (it can be empty)</param>
        /// <returns></returns>
        private bool GetProperty(Property a_property, out string a_szProperty)
        {
            // Init stuff...
            a_szProperty = "";
            
            // Validate the arguments...
	        if (a_property == null)
	        {
                Log.Error("GetProperty: null argument...");
		        return (false);
	        }

	        // No name (we get this with the root and with arrays)...
	        if (a_property.u32PropertyLength == 0)
	        {
		        return (true);
	        }

	        // Copy the property, losing the quotes...
            if ((m_szJson[(int)a_property.u32PropertyOffset] == '"') || (m_szJson[(int)a_property.u32PropertyOffset] == '\''))
            {
                a_szProperty = m_szJson.Substring((int)(a_property.u32PropertyOffset + 1), (int)(a_property.u32PropertyLength - 2));
            }

            // Under relaxed mode, handle escaped quotes...
            else if (   ((a_property.u32PropertyOffset + 1) < m_szJson.Length)
                     && ((m_szJson.Substring((int)a_property.u32PropertyOffset,2) == "\\\"")
                     ||  (m_szJson.Substring((int)a_property.u32PropertyOffset,2) == "\\'")))
            {
                a_szProperty = m_szJson.Substring((int)(a_property.u32PropertyOffset + 2), (int)(a_property.u32PropertyLength - 4));
            }

            // Under relaxed mode, we may not have quotes to lose...
            else
            {
                a_szProperty = m_szJson.Substring((int)a_property.u32PropertyOffset, (int)a_property.u32PropertyLength);
            }

	        // All done...
	        return (true);
        }

        /// <summary>
        /// Get a value.  When the JSON rules are relaxed we allow for the
        /// following combinations:
        ///
        ///     "property": ...
        ///     'property': ...
        ///     \"property\": ...
        ///     \'property\': ...
        ///
        /// </summary>
        /// <param name="a_property">our place in the tree</param>
        /// <param name="a_szValue">whatever we find (it can be empty)</param>
        /// <param name="a_epropertytype">the type of the item we found</param>
        /// <returns></returns>
        private bool GetValue(Property a_property, out string a_szValue, out EPROPERTYTYPE a_epropertytype)
        {
            // Clear the target...
            a_szValue = "";
            a_epropertytype = EPROPERTYTYPE.UNDEFINED;
            
            // Validate the arguments...
	        if (a_property == null)
	        {
                Log.Error("GetValue: null argument...");
		        return (false);
	        }

	        // Handle strings...
	        if (a_property.epropertytype == EPROPERTYTYPE.STRING)
	        {
		        // Empty string...
		        if (a_property.u32ValueLength == 0)
		        {
                    a_epropertytype = a_property.epropertytype;
			        return (true);
		        }

                // Handle escaped quotes...
                else if (   ((a_property.u32ValueOffset + 1) < m_szJson.Length)
                         && ((m_szJson.Substring((int)a_property.u32ValueOffset, 2) == "\\\"")
                         ||  (m_szJson.Substring((int)a_property.u32ValueOffset, 2) == "\\'")))
                {
                    a_szValue = m_szJson.Substring((int)(a_property.u32ValueOffset + 2), (int)(a_property.u32ValueLength - 4));
                }

                // Handle regular quotes...
                else
                {
                    // All we have is "" (an empty string)...
                    if (a_property.u32ValueLength == 2)
                    {
                        a_szValue = "";
                    }
                    // We have data in our quotes...
                    else
                    {
                        a_szValue = m_szJson.Substring((int)(a_property.u32ValueOffset + 1), (int)(a_property.u32ValueLength - 2));
                    }
                }
	        }

	        // Handle everything else...
	        else
	        {
                // Copy the entire block of data (whole objects and arrays included)...
                a_szValue = m_szJson.Substring((int)a_property.u32ValueOffset, (int)a_property.u32ValueLength);
            }

	        // All done...
            a_epropertytype = a_property.epropertytype;
	        return (true);
        }

        /// <summary>
        /// Work our way through an object...
        /// </summary>
        /// <param name="a_property">current place in the tree</param>
        /// <param name="a_u32Json">current offset into the JSON string</param>
        /// <returns></returns>
        private bool ParseObject(Property a_property, ref UInt32 a_u32Json)
        {
	        UInt32 u32Json;
	        UInt32 u32ValueOffset;
	        Property property;
	        Property propertyPrev;

	        // Init stuff...
	        u32Json = a_u32Json;

	        // We have to start with an open square bracket...
	        if (m_szJson[(int)u32Json] != '{')
	        {
                Log.Error("ParseObject: expected open curly...");
		        return (false);
	        }

	        // Make a note of where we are...
	        u32ValueOffset = u32Json;

	        // Skip the curly...
	        u32Json += 1;

            // Clear any whitespace...
            if (!SkipWhitespace(ref u32Json))
            {
                Log.Error("ParseObject: we ran out of data...");
                a_u32Json = u32Json;
                return (false);
            }

	        // We're an empty object...
	        if (m_szJson[(int)u32Json] == '}')
	        {
		        a_property.epropertytype = EPROPERTYTYPE.OBJECT;
		        a_property.u32ValueOffset = u32ValueOffset;
		        a_property.u32ValueLength = (u32Json + 1) - a_property.u32ValueOffset;
		        a_u32Json = u32Json + 1;
		        return (true);
	        }

	        // Loopy...
	        propertyPrev = a_property;
	        for (; u32Json < m_szJson.Length; u32Json++)
	        {
		        // Create a new record...
		        property = new Property();

		        // First kiddie in the list, so it's our child...
		        if (a_property.propertyChild == null)
		        {
			        a_property.propertyChild = property;
			        propertyPrev = a_property.propertyChild;
		        }
		        // Append to the end of our child's sibling list...
		        else
		        {
			        propertyPrev.propertySibling = property;
			        propertyPrev = propertyPrev.propertySibling;
		        }

                // Clear any whitespace...
                if (!SkipWhitespace(ref u32Json))
                {
                    Log.Error("ParseObject: we ran out of data...");
                    a_u32Json = u32Json;
                    return (false);
                }

		        // This needs to be a property name...
		        if (!ParseString(property, ref u32Json, false))
		        {
                    Log.Error("ParseObject: ParseString failed...");
			        a_u32Json = u32Json;
			        return (false);
		        }

                // Clear any whitespace...
                if (!SkipWhitespace(ref u32Json))
                {
                    a_u32Json = u32Json;
                    Log.Error("ParseObject: we ran out of data...");
                    return (false);
                }

		        // We need a colon...
		        if (m_szJson[(int)u32Json] != ':')
		        {
                    Log.Error("ParseObject: expected a colon...");
			        a_u32Json = u32Json;
			        return (false);
		        }

		        // Clear the colon...
		        u32Json += 1;

                // Clear any whitespace...
                if (!SkipWhitespace(ref u32Json))
                {
                    a_u32Json = u32Json;
                    Log.Error("ParseObject: we ran out of data...");
                    return (false);
                }

		        // This needs to be a value...
		        if (!ParseValue(property, ref u32Json))
		        {
                    Log.Error("ParseObject: ParseValue failed...");
			        a_u32Json = u32Json;
			        return (false);
		        }

                // Clear any whitespace...
                if (!SkipWhitespace(ref u32Json))
                {
                    Log.Error("ParseObject: we ran out of data...");
                    a_u32Json = u32Json;
                    return (false);
                }

		        // If we see a comma, we have more coming...
		        if (m_szJson[(int)u32Json] == ',')
		        {
			        continue;
		        }

		        // If we see a closing square bracket, then we're done...
		        if (m_szJson[(int)u32Json] == '}')
		        {
			        a_property.epropertytype = EPROPERTYTYPE.OBJECT;
			        a_property.u32ValueOffset = u32ValueOffset;
			        a_property.u32ValueLength = (u32Json + 1) - a_property.u32ValueOffset;
			        a_u32Json = u32Json + 1;
			        return (true);
		        }

		        // Uh-oh...
		        break;
	        }

	        // Uh-oh...
            Log.Error("ParseObject: expected a closing curly...");
	        a_u32Json = u32Json;
	        return (false);
        }

        /// <summary>
        /// Work our way through an array...
        /// </summary>
        /// <param name="a_property">current place in the tree</param>
        /// <param name="a_u32Json">current offset into the JSON string</param>
        /// <returns></returns>
        private bool ParseArray(Property a_property, ref UInt32 a_u32Json)
        {
	        UInt32 u32Json;
	        UInt32 u32ValueOffset;
	        Property property;
	        Property propertyPrev;

	        // Init stuff...
	        u32Json = a_u32Json;

	        // We have to start with an open square bracket...
	        if (m_szJson[(int)u32Json] != '[')
	        {
                Log.Error("ParseArray: expected a open square bracket...");
		        return (false);
	        }

	        // Make a note of where we are...
	        u32ValueOffset = u32Json;

	        // Skip the bracket...
	        u32Json += 1;

            // Clear any whitespace...
            if (!SkipWhitespace(ref u32Json))
            {
                Log.Error("ParseObject: we ran out of data...");
                a_u32Json = u32Json;
                return (false);
            }

	        // We're an empty array...
	        if (m_szJson[(int)u32Json] == ']')
	        {
		        a_property.epropertytype = EPROPERTYTYPE.ARRAY;
		        a_property.u32ValueOffset = u32ValueOffset;
		        a_property.u32ValueLength = (u32Json + 1) - a_property.u32ValueOffset;
		        a_u32Json = u32Json + 1;
		        return (true);
	        }

	        // Loopy...
	        propertyPrev = a_property;
            for (; u32Json < m_szJson.Length; u32Json++)
	        {
		        // Create a new record...
		        property = new Property();

		        // First kiddie in the list, so it's our child...
		        if (a_property.propertyChild == null)
		        {
			        a_property.propertyChild = property;
			        propertyPrev = a_property.propertyChild;
		        }
		        // Append to the end of our child's sibling list...
		        else
		        {
			        propertyPrev.propertySibling = property;
			        propertyPrev = propertyPrev.propertySibling;
		        }

                // Clear any whitespace...
                if (!SkipWhitespace(ref u32Json))
                {
                    a_u32Json = u32Json;
                    Log.Error("ParseObject: we ran out of data...");
                    return (false);
                }

		        // This needs to be a value...
		        if (!ParseValue(property, ref u32Json))
		        {
                    Log.Error("ParseArray: ParseValue failed...");
			        a_u32Json = u32Json;
			        return (false);
		        }

		        // Clear any whitespace...
                if (!SkipWhitespace(ref u32Json))
                {
                    Log.Error("ParseArray: we ran out of data...");
                    a_u32Json = u32Json;
                    return (false);
                }

		        // If we see a comma, we have more coming...
		        if (m_szJson[(int)u32Json] == ',')
		        {
			        continue;
		        }

		        // If we see a closing square bracket, then we're done...
		        if (m_szJson[(int)u32Json] == ']')
		        {
			        a_property.epropertytype  = EPROPERTYTYPE.ARRAY;
			        a_property.u32ValueOffset = u32ValueOffset;
			        a_property.u32ValueLength = (u32Json + 1) - a_property.u32ValueOffset;
			        a_u32Json = u32Json + 1;
			        return (true);
		        }

		        // Uh-oh...
		        break;
	        }

	        // Uh-oh...
            Log.Error("ParseArray: expected a closing square bracket...");
	        a_u32Json = u32Json;
	        return (false);
        }

        /// <summary>
        /// Work our way through a value...
        /// </summary>
        /// <param name="a_property">current place in the tree</param>
        /// <param name="a_u32Json">current offset into the JSON string</param>
        /// <returns></returns>
        private bool ParseValue(Property a_property, ref UInt32 a_u32Json)
        {
	        UInt32 u32Json = a_u32Json;

	        switch (m_szJson[(int)u32Json])
	        {
		        // Well, that wasn't value...
		        default:
                    Log.Error("ParseValue: unexpected token at (" + u32Json + ")...<" + m_szJson[(int)u32Json] + ">");
			        return (false);

		        // A string or an escaped string...
		        case '"': case '\'': case '\\':
			        return (ParseString(a_property, ref a_u32Json, true));

		        // A number...
		        case '-':
		        case '0': case '1': case '2': case '3': case '4':
		        case '5': case '6': case '7': case '8': case '9':
			        return (ParseNumber(a_property, ref a_u32Json));

		        // An object...
		        case '{':
			        return (ParseObject(a_property, ref a_u32Json));

		        // An array...
		        case '[':
			        return (ParseArray(a_property, ref a_u32Json));

		        // A boolean true...
		        case 't':
			        if (m_szJson[(int)(u32Json + 1)] != 'r')
			        {
                        Log.Error("ParseValue: it ain't tRue...");
				        a_u32Json = u32Json + 1;
				        return (false);
			        }
			        if (m_szJson[(int)(u32Json + 2)] != 'u')
			        {
                        Log.Error("ParseValue: it ain't trUe...");
				        a_u32Json = u32Json + 2;
				        return (false);
			        }
                    if (m_szJson[(int)(u32Json + 3)] != 'e')
			        {
                        Log.Error("ParseValue: it ain't truE...");
				        a_u32Json = u32Json + 3;
				        return (false);
			        }
			        a_u32Json = u32Json + 4;
			        a_property.epropertytype = EPROPERTYTYPE.BOOLEAN;
			        a_property.u32ValueOffset = u32Json;
			        a_property.u32ValueLength = 4;
			        return (true);

		        // A boolean false...
		        case 'f':
                    if (m_szJson[(int)(u32Json + 1)] != 'a')
			        {
                        Log.Error("ParseValue: it ain't fAlse...");
				        a_u32Json = u32Json + 1;
				        return (false);
			        }
                    if (m_szJson[(int)(u32Json + 2)] != 'l')
			        {
                        Log.Error("ParseValue: it ain't faLse...");
				        a_u32Json = u32Json + 2;
				        return (false);
			        }
                    if (m_szJson[(int)(u32Json + 3)] != 's')
			        {
                        Log.Error("ParseValue: it ain't falSe...");
				        a_u32Json = u32Json + 3;
				        return (false);
			        }
                    if (m_szJson[(int)(u32Json + 4)] != 'e')
			        {
                        Log.Error("ParseValue: it ain't falsE...");
				        a_u32Json = u32Json + 4;
				        return (false);
			        }
			        a_u32Json = u32Json + 5;
			        a_property.epropertytype = EPROPERTYTYPE.BOOLEAN;
			        a_property.u32ValueOffset = u32Json;
			        a_property.u32ValueLength = 5;
			        return (true);

		        // A boolean null...
		        case 'n':
                    if (m_szJson[(int)(u32Json + 1)] != 'u')
			        {
                        Log.Error("ParseValue: it ain't nUll...");
				        a_u32Json = u32Json + 1;
				        return (false);
			        }
                    if (m_szJson[(int)(u32Json + 2)] != 'l')
			        {
                        Log.Error("ParseValue: it ain't nuLl...");
				        a_u32Json = u32Json + 2;
				        return (false);
			        }
                    if (m_szJson[(int)(u32Json + 3)] != 'l')
			        {
                        Log.Error("ParseValue: it ain't nulL...");
				        a_u32Json = u32Json + 3;
				        return (false);
			        }
			        a_u32Json = u32Json + 4;
			        a_property.epropertytype = EPROPERTYTYPE.NULL;
			        a_property.u32ValueOffset = u32Json;
			        a_property.u32ValueLength = 4;
			        return (true);
	        }
        }

        /// <summary>
        /// Work our way through a string (property or value)...
        /// </summary>
        /// <param name="a_property">current place in the tree</param>
        /// <param name="a_u32Json">current offset into the JSON string</param>
        /// <param name="a_blValue">true if a value</param>
        /// <returns></returns>
        bool ParseString(Property a_property, ref UInt32 a_u32Json, bool a_blValue)
        {
            string szQuote;
	        UInt32 u32Json = a_u32Json;

	        // Under strict rules the first character must be a doublequote...
            if (m_blStrictParsingRules)
            {
                if (m_szJson[(int)u32Json] != '"')
                {
                    Log.Error("ParseString: expected a opening doublequote...");
                    return (false);
                }
                szQuote = m_szJson[(int)u32Json].ToString();
            }

            // Come here for relaxed rules...
            else
            {
                // Handle escaped quotes...
                if (((u32Json + 1) < m_szJson.Length) && ((m_szJson.Substring((int)u32Json,2) == "\\\"") || (m_szJson.Substring((int)u32Json,2) == "\\'")))
                {
                    szQuote = m_szJson.Substring((int)u32Json,2);
                }

                // A value must have an opening quote (double or single), or
                // if we detect that we have a quote, then pop in here...
                else if (a_blValue || (m_szJson[(int)u32Json] == '"') || (m_szJson[(int)u32Json] == '\''))
                {
                    if ((m_szJson[(int)u32Json] != '"') && (m_szJson[(int)u32Json] != '\''))
                    {
                        Log.Error("ParseString: expected an opening quote...");
                        return (false);
                    }
                    szQuote = m_szJson[(int)u32Json].ToString();
                }

                // A property name can have quotes or be an alphanumeric, and underscore or a dollarsign...
                else
                {
                    if (!Char.IsLetterOrDigit(m_szJson[(int)u32Json]) && (m_szJson[(int)u32Json] != '_') && (m_szJson[(int)u32Json] != '$'))
                    {
                        Log.Error("ParseString: expected a valid property name...");
                        return (false);
                    }
                    szQuote = m_szJson[(int)u32Json].ToString();
                }
            }

	        // Init stuff...
	        if (a_blValue)
	        {
		        a_property.u32ValueOffset = u32Json;
	        }
	        else
	        {
		        a_property.u32PropertyOffset = u32Json;
	        }

            // Clear the quote if we found one...
            if ((szQuote == "\\\"") || (szQuote == "\\'"))
            {
                u32Json += 2;
            }
            else if ((szQuote == "\"") || (szQuote == "'"))
            {
                u32Json += 1;
            }

	        // Loopy...
	        for (; u32Json < m_szJson.Length; u32Json++)
	        {
		        // Fail on a control character...
		        if (Char.IsControl(m_szJson[(int)u32Json]))
		        {
                    Log.Error("ParseString: detected a control character...");
			        a_u32Json = u32Json;
			        return (false);
		        }

		        // Under strict rules a doublequote ends us...
                if (m_blStrictParsingRules)
                {
                    if (m_szJson[(int)u32Json] == '"')
                    {
                        if (a_blValue)
                        {
                            a_property.u32ValueLength = (u32Json + 1) - a_property.u32ValueOffset;
                            a_property.epropertytype = EPROPERTYTYPE.STRING;
                        }
                        else
                        {
                            a_property.u32PropertyLength = (u32Json + 1) - a_property.u32PropertyOffset;
                            // Don't change the type, we could be anything...
                        }
                        a_u32Json = u32Json + 1;
                        return (true);
                    }
                }

                // Under relaxed rules we'll end on a matching escaped quite...
                else if (((u32Json + 1) < m_szJson.Length) && (szQuote == (m_szJson.Substring((int)u32Json, 2))))
                {
                    if (a_blValue)
                    {
                        a_property.u32ValueLength = (u32Json + 2) - a_property.u32ValueOffset;
                        a_property.epropertytype = EPROPERTYTYPE.STRING;
                    }
                    else
                    {
                        a_property.u32PropertyLength = (u32Json + 2) - a_property.u32PropertyOffset;
                        // Don't change the type, we could be anything...
                    }
                    a_u32Json = u32Json + 2;
                    return (true);
                }

                // Under relaxed rules we'll end on a matching quote, if we have one, this
                // path is guaranteed to catch the closing quote on a value...
                else if ((m_szJson[(int)u32Json].ToString() == szQuote) && ((szQuote == "\"") || (szQuote == "'")))
                {
                    if (a_blValue)
                    {
                        a_property.u32ValueLength = (u32Json + 1) - a_property.u32ValueOffset;
                        a_property.epropertytype = EPROPERTYTYPE.STRING;
                    }
                    else
                    {
                        a_property.u32PropertyLength = (u32Json + 1) - a_property.u32PropertyOffset;
                        // Don't change the type, we could be anything...
                    }
                    a_u32Json = u32Json + 1;
                    return (true);
                }

                // Otherwise, (still under relaxed rules) if we're a property name, and we
                // didn't open on a quote, we'll end on anything that isn't alphanumeric,
                // an underscore or a dollarsign...
                else if (   !a_blValue
                         && (szQuote != "\"")
                         && (szQuote != "'")
                         && !Char.IsLetterOrDigit(m_szJson[(int)u32Json])
                         && (m_szJson[(int)u32Json] != '_')
                         && (m_szJson[(int)u32Json] != '$'))
                {
                    a_property.u32PropertyLength = u32Json - a_property.u32PropertyOffset;
                    // Don't change the type, we could be anything...
                    // Don't skip over this item...
                    a_u32Json = u32Json;
                    return (true);
                }

		        // If we're not a backslash, we're okay...
		        if (m_szJson[(int)u32Json] != '\\')
		        {
			        continue;
		        }

		        // Handle escape characters...
		        u32Json += 1;
		        switch (m_szJson[(int)u32Json])
		        {
			        default:
                        Log.Error("ParseString: bad escape character at (" + u32Json + ")...<" + m_szJson[(int)u32Json] + ">");
				        a_u32Json = u32Json;
				        return (false);

			        case '"': case '\\': case '/':
			        case 'n':  case 'r': case 't': case 'b': case 'f':
				        continue;

			        // Make sure we have at least four of these in a row...
			        case 'u':
				        if (!IsXDigit(m_szJson[(int)(u32Json + 1)]))
				        {
                            Log.Error("ParseString: it ain't a \\uXxxx");
					        a_u32Json = u32Json + 1;
					        return (false);
				        }
				        if (!IsXDigit(m_szJson[(int)(u32Json + 2)]))
				        {
                            Log.Error("ParseString: it ain't a \\uxXxx");
					        a_u32Json = u32Json + 2;
					        return (false);
				        }
				        if (!IsXDigit(m_szJson[(int)(u32Json + 3)]))
				        {
                            Log.Error("ParseString: it ain't a \\uxxXx");
					        a_u32Json = u32Json + 3;
					        return (false);
				        }
				        if (!IsXDigit(m_szJson[(int)(u32Json + 4)]))
				        {
                            Log.Error("ParseString: it ain't a \\uxxxX");
					        a_u32Json = u32Json + 4;
					        return (false);
				        }
				        a_u32Json = u32Json + 5;
				        continue;
		        }
	        }

	        // Uh-oh...
            Log.Error("ParseString: expected a closing quote or something...");
	        a_u32Json = u32Json;
	        return (false);
        }

        /// <summary>
        /// Work our way through a number...
        /// </summary>
        /// <param name="a_property">current place in the tree</param>
        /// <param name="a_u32Json">current offset into the JSON string</param>
        /// <returns></returns>
        bool ParseNumber(Property a_property,ref UInt32 a_u32Json)
        {
	        bool blDecimalDetected;
	        bool blExponentDetected;
	        bool blExponentSignDetected;
	        bool blExponentDigitDetected;
	        bool blLeadingZero;
	        bool blNonZeroDigitDetected;
	        UInt32 u32Json;

	        // Init stuff...
	        blDecimalDetected = false;
	        blExponentDetected = false;
	        blExponentSignDetected = false;
	        blExponentDigitDetected = false;
	        blLeadingZero = false;
	        blNonZeroDigitDetected = false;
	        a_property.u32ValueOffset = a_u32Json;

	        // Loopy...
	        for (u32Json = a_u32Json; u32Json < m_szJson.Length; u32Json++)
	        {
		        // Detect termination of the number and watch for illegal values...
		        switch (m_szJson[(int)u32Json])
		        {
			        // We've a problem...
			        default:
                        Log.Error("ParseNumber: not a valid token in a number...");
				        a_u32Json = u32Json;
				        return (false);

			        // We're done (and okay) on the following...
			        case ' ': case '\t': case '\r': case '\n':
			        case ',': case '}': case ']':
				        // Bad exponent...
				        if (blExponentDetected && !blExponentDigitDetected)
				        {
                            Log.Error("ParseNumber: bad exponent...");
					        a_u32Json = u32Json;
					        return (false);
				        }

				        // Don't skip past this value, the function above us needs to be able to check it...
				        a_u32Json = u32Json;
				        a_property.epropertytype = EPROPERTYTYPE.NUMBER;
				        a_property.u32ValueLength = u32Json - a_property.u32ValueOffset;
				        return (true);

			        // We're good...
			        case '-': case '.': case '+': case 'e': case 'E':
			        case '0': case '1': case '2': case '3': case '4':
			        case '5': case '6': case '7': case '8': case '9':
				        break;
		        }

		        // Fail on embedded or trailing minus (not part of exponent, that's further down)...
		        // good: 1-23
		        // bad: 1-, 1-123
		        if (!blExponentDetected && (m_szJson[(int)u32Json] == '-'))
		        {
			        if ((m_szJson[(int)u32Json] == '-') && ((u32Json != a_u32Json) || (u32Json >= m_szJson.Length)))
			        {
                        Log.Error("ParseNumber: problem with how minus is being used...");
				        a_u32Json = u32Json;
				        return (false);
			        }
			        continue;
		        }

		        // Detect a leading zero...
		        if (!blNonZeroDigitDetected && (m_szJson[(int)u32Json] == '0'))
		        {
			        // We can be the first or second item in the string...
			        if ((u32Json == a_u32Json) || (u32Json == (a_u32Json + 1)))
			        {
				        blLeadingZero = true;
				        continue;
			        }
                    Log.Error("ParseNumber: found a leading zero...");
			        a_u32Json = u32Json;
			        return (false);
		        }

		        // Fail on a leading zero...
		        // ex: 000, 0123
		        if (blLeadingZero && !blNonZeroDigitDetected && Char.IsDigit(m_szJson[(int)u32Json]))
		        {
                    Log.Error("ParseNumber: found a leading zero...");
			        a_u32Json = u32Json;
			        return (false);
		        }

		        // Fail on multiple decimals or a decimal with no leading digit...
		        if (m_szJson[(int)u32Json] == '.')
		        {
			        if (	blDecimalDetected
				        ||	(!blLeadingZero && !blNonZeroDigitDetected))
			        {
                        Log.Error("ParseNumber: bad decimal point...");
				        a_u32Json = u32Json;
				        return (false);
			        }
			        // Clear the leading zero check, we don't need this anymore...
			        blLeadingZero = false;
			        blDecimalDetected = true;
			        continue;
		        }

		        // Fail on multiple exponent or an exponent with no leading digit...
		        if ((m_szJson[(int)u32Json] == 'e') || (m_szJson[(int)u32Json] == 'E'))
		        {
			        if (	blExponentDetected
				        ||	(!blLeadingZero && !blNonZeroDigitDetected))
			        {
                        Log.Error("ParseNumber: bad exponent...");
				        a_u32Json = u32Json;
				        return (false);
			        }
			        blExponentDetected = true;
			        continue;
		        }

		        // Fail on multiple exponent sign, or sign with no leading exponent,
		        // or sign after exponent digit...
		        if ((m_szJson[(int)u32Json] == '+') || (m_szJson[(int)u32Json] == '-'))
		        {
			        if (	blExponentSignDetected
				        ||	!blExponentDetected
				        ||	blExponentDigitDetected)
			        {
                        Log.Error("ParseNumber: bad exponent...");
				        a_u32Json = u32Json;
				        return (false);
			        }
			        blExponentSignDetected = true;
			        continue;
		        }

		        // Detected an integer digit...
		        if (!blDecimalDetected && !blExponentDetected)
		        {
			        switch (m_szJson[(int)u32Json])
			        {
				        default:
					        break;
				        case '-': case '.': case '+': case 'e': case 'E':
				        case '0': case '1': case '2': case '3': case '4':
				        case '5': case '6': case '7': case '8': case '9':
					        blNonZeroDigitDetected = true;
					        continue;
			        }
		        }

		        // Make sure we catch decimal numbers...
		        if (Char.IsDigit(m_szJson[(int)u32Json]))
		        {
			        if (!blExponentDetected)
			        {
				        blNonZeroDigitDetected = true;
			        }
			        else
			        {
				        blExponentDigitDetected = true;
			        }
		        }
	        }

	        // Uh-oh...
            Log.Error("ParseNumber: problem with a number...");
	        a_u32Json = u32Json;
	        return (false);
        }

        /// <summary>
        /// Skip whitespace in the JSON string...
        /// </summary>
        /// <param name="a_u32Json">index to move</param>
        /// <returns>false if we run out of string</returns>
        private bool SkipWhitespace(ref UInt32 a_u32Json)
        {
	        // Loopy...
	        while (a_u32Json < m_szJson.Length)
	        {
		        if (!Char.IsWhiteSpace(m_szJson[(int)a_u32Json]))
		        {
			        return (true);
		        }
                a_u32Json += 1;
	        }

	        // We ran out of data...
	        return (false);
        }

        /// <summary>
        /// Free resources...
        /// </summary>
        private void Unload()
        {
            m_szJson = null;

	        if (m_property != null)
	        {
		        FreeProperty(m_property);
		        m_property = null;
	        }
        }

        /// <summary>
        /// C# leaves out the most amazing stuff...
        /// </summary>
        /// <param name="c">character to check</param>
        /// <returns>true if its a hexit</returns>
        private static bool IsXDigit(char c)
        {
            if (Char.IsDigit(c)) return true;
            if ((c >= 'a') && (c <= 'f')) return true;
            if ((c >= 'A') && (c <= 'F')) return true;
            return false;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        /// <summary>
        /// Our main data structure for holding onto the information about
        /// a JSON string after we've loaded it...
        /// </summary>
        private class Property
        {
            // Init stuff...
            public Property()
            {
 	            propertySibling = null;
	            propertyChild = null;
	            epropertytype = EPROPERTYTYPE.UNDEFINED;
	            u32PropertyOffset = 0;
	            u32PropertyLength = 0;
	            u32ValueOffset = 0;
	            u32ValueLength = 0;
            }

            // Our attributes...
	        public Property		    propertySibling;
	        public Property		    propertyChild;
	        public EPROPERTYTYPE	epropertytype;
	        public UInt32		    u32PropertyOffset;
	        public UInt32		    u32PropertyLength;
	        public UInt32		    u32ValueOffset;
	        public UInt32		    u32ValueLength;
        };

        /// <summary>
        /// Key/Value pair structure, used to override the
        /// values of keys during a Dump()...
        /// </summary>
        private class KeyValue
        {
            public string szKey;
            public string szValue;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Attributes...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Attributes...

        /// <summary>
        /// A place to store the JSON string while we work with it...
        /// </summary>
        private string m_szJson;

        /// <summary>
        /// A place to store the load data on the JSON string...
        /// </summary>
        private Property m_property;

        /// <summary>
        /// If false, then property names don't have to have quotes
        /// as long as they don't have any embedded whitespace, and
        /// single-quotes are allowed.  This makes it easier on folks
        /// generating JSON from scripted languages, or if they use
        /// command line tools like cURL...
        /// </summary>
        private bool m_blStrictParsingRules;

        /// <summary>
        /// List of changes to the JSON in key/value pairs...
        /// </summary>
        private List<KeyValue> m_lkeyvalueOverride;

        #endregion
    }
}
