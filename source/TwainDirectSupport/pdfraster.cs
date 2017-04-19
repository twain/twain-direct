///////////////////////////////////////////////////////////////////////////////////////
//
// TwainDirectSupport.PdfRaster
//
// PDF/raster writer functions.  These are implemented as close as possible to the
// C modules written by Atalasoft.  The main deviation is in the callback functions.
// We're not trying to support a heap manager in this version of the code.
//
///////////////////////////////////////////////////////////////////////////////////////
//  Author          Date            Version     Comment
//  M.McLaughlin    19-Dec-2014     0.0.0.1     Initial Transcription from C
///////////////////////////////////////////////////////////////////////////////////////
//  Copyright (C) 2014-2016 Kodak Alaris Inc.
//  Copyright (C) 2014-2015 Atalasoft, Inc.
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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace TwainDirectSupport
{
    public sealed class PdfRaster
    {
        ///////////////////////////////////////////////////////////////////////////////
        // Public Methods: PdfRaster
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Methods: PdfRaster

        /// <summary>
        /// Add image header to the image data from a PDF/raster..
        /// </summary>
        /// <param name="a_abImage">the image data</param>
        /// <param name="a_rasterpixelformat">color, grayscale, black and white</param>
        /// <param name="a_rastercompression">none. group4, jpeg</param>
        /// <param name="a_lResolution">resolution in dots per inch</param>
        /// <param name="a_lWidth">width in pixels</param>
        /// <param name="a_lHeight">height in pixels</param>
        /// <returns>true on success</returns>
        public static bool AddImageHeader
        (
            out byte[] a_abImage,
            byte[] a_abStripData,
            PdfRasterReader.Reader.PdfRasterReaderPixelFormat a_rasterpixelformat,
            PdfRasterReader.Reader.PdfRasterReaderCompression a_rastercompression,
            long a_lResolution,
            long a_lWidth,
            long a_lHeight
        )
        {
            int iCount = (int) (a_lWidth * a_lHeight);
            int iHeader;
            IntPtr intptr;
            TiffBitonalUncompressed tiffbitonaluncompressed;
            TiffBitonalG4 tiffbitonalg4;
            TiffGrayscaleUncompressed tiffgrayscaleuncompressed;
            TiffColorUncompressed tiffcoloruncompressed;

            // Init stuff...
            a_abImage = null;

            // Add a header, if needed...
            switch (a_rasterpixelformat)
            {
                default:
                    return (false);
                    
                case PdfRasterReader.Reader.PdfRasterReaderPixelFormat.PDFRASREAD_BITONAL:
                    switch (a_rastercompression)
                    {
                        default:
                            return (false);
                            
                        case PdfRasterReader.Reader.PdfRasterReaderCompression.PDFRASREAD_UNCOMPRESSED:
                            iCount = (int)(((a_lWidth + 7) / 8) * a_lHeight); //it's packed
                            tiffbitonaluncompressed = new TiffBitonalUncompressed((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffbitonaluncompressed);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffbitonaluncompressed, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                            
                        case PdfRasterReader.Reader.PdfRasterReaderCompression.PDFRASEARD_CCITTG4:
                            tiffbitonalg4 = new TiffBitonalG4((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffbitonalg4);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffbitonalg4, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                    }
                    break;
                    
                case PdfRasterReader.Reader.PdfRasterReaderPixelFormat.PDFRASREAD_GRAYSCALE:
                    switch (a_rastercompression)
                    {
                        default:
                            return (false);
                            
                        case PdfRasterReader.Reader.PdfRasterReaderCompression.PDFRASREAD_UNCOMPRESSED:
                            tiffgrayscaleuncompressed = new TiffGrayscaleUncompressed((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffgrayscaleuncompressed);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffgrayscaleuncompressed, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                            
                        case PdfRasterReader.Reader.PdfRasterReaderCompression.PDFRASREAD_JPEG:
                            iHeader = 0;
                            iCount = a_abStripData.Length;
                            a_abImage = new byte[iCount];
                            File.WriteAllBytes("fu-gray.jpg", a_abStripData);
                            break;
                    }
                    break;
                    
                case PdfRasterReader.Reader.PdfRasterReaderPixelFormat.PDFRASREAD_RGB:
                    switch (a_rastercompression)
                    {
                        default:
                            return (false);
                            
                        case PdfRasterReader.Reader.PdfRasterReaderCompression.PDFRASREAD_UNCOMPRESSED:
                            iCount *= 3; // 3 samples per pixel
                            tiffcoloruncompressed = new TiffColorUncompressed((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffcoloruncompressed);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffcoloruncompressed, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                            
                        case PdfRasterReader.Reader.PdfRasterReaderCompression.PDFRASREAD_JPEG:
                            iHeader = 0;
                            iCount = a_abStripData.Length;
                            a_abImage = new byte[iCount];
                            File.WriteAllBytes("fu-color.jpg", a_abStripData);
                            break;
                    }
                    break;
            }
            
            // Copy the image data...
            Buffer.BlockCopy(a_abStripData, 0, a_abImage, iHeader, iCount);

            // All done...
            return (true);
        }

        /// <summary>
        /// Extract the image data from a PDF/raster.  This is a quick and dirty way of
        /// getting at the image without using a fully functional PDF library...
        /// </summary>
        /// <param name="a_szPdfRaster">the PDF raster file to use</param>
        /// <param name="a_abImage">the image data</param>
        /// <param name="a_rasterpixelformat">color, grayscale, black and white</param>
        /// <param name="a_rastercompression">none. group4, jpeg</param>
        /// <param name="a_lResolution">resolution in dots per inch</param>
        /// <param name="a_lWidth">width in pixels</param>
        /// <param name="a_lHeight">height in pixels</param>
        /// <returns>true on success</returns>
        public static bool GetImage
        (
            string a_szPdfRaster,
            out byte[] a_abImage,
            out PdfRasterWriter.Writer.PdfRasterPixelFormat a_rasterpixelformat,
            out PdfRasterWriter.Writer.PdfRasterCompression a_rastercompression,
            out long a_lResolution,
            out long a_lWidth,
            out long a_lHeight
        )
        {
            int iIndex;
            int iCount;
            int iHeader;
            int iBitsPerComponent;
            IntPtr intptr;
            byte[] abPdfRaster;
            TiffBitonalUncompressed tiffbitonaluncompressed;
            TiffBitonalG4 tiffbitonalg4;
            TiffGrayscaleUncompressed tiffgrayscaleuncompressed;
            TiffColorUncompressed tiffcoloruncompressed;

            // Init stuff...
            a_abImage = null;
            a_rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL;
            a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED;
            a_lResolution = 0;
            a_lWidth = 0;
            a_lHeight = 0;

            // Read the data...
            try
            {
                abPdfRaster = File.ReadAllBytes(a_szPdfRaster);
            }
            catch
            {
                return (false);
            }

            // We have a group4 compressed bitonal image...
            if (GetIndex(abPdfRaster, 0, 512, "/CCITTFaxDecode") > 0)
            {
                a_rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL;
                a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_CCITTG4;
            }

            // We have a grayscale or an uncompressed black-and-white image...
            else if (GetIndex(abPdfRaster, 0, 512, "/DeviceGray") > 0)
            {
                // We're looking for a value of 1 or 8...
                iIndex = GetIndex(abPdfRaster, 0, 512, "/BitsPerComponent ");
                if (iIndex < 0) return (false);
                iIndex += 18;
                iCount = GetIndex(abPdfRaster, iIndex, 512, " ");
                if (iCount < 0) return (false);
                iCount -= iIndex;
                iBitsPerComponent = int.Parse(Encoding.UTF8.GetString(abPdfRaster, iIndex, iCount));

                // We're an uncompressed black-and-white image...
                if (iBitsPerComponent == 1)
                {
                    a_rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL;
                    a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED;
                }

                // We're a jpeg or uncompressed grayscale image...
                else if (iBitsPerComponent == 8)
                {

                    // a_rasterpixelformat = RasterPixelFormat.PDFRAS_GRAYSCALE;
                    a_rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_GRAYSCALE;
                    if (GetIndex(abPdfRaster, 0, 512, "/DCTDecode") > 0)
                    {
                        // a_rastercompression = RasterCompression.PDFRAS_JPEG;
                        a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_JPEG;
                    }
                    else
                    {
                        // a_rastercompression = RasterCompression.PDFRAS_UNCOMPRESSED;
                        a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED;
                    }
                }

                // Uh-oh...
                else
                {
                    return (false);
                }
            }

            // We have a jpeg or uncompressed color image...
            else if (GetIndex(abPdfRaster, 0, 512, "/DeviceRGB") > 0)
            {
                a_rasterpixelformat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_RGB;
                if (GetIndex(abPdfRaster, 0, 512, "/DCTDecode") > 0)
                {
                    a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_JPEG;
                }
                else
                {
                    a_rastercompression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED;
                }
            }

            // Hmmmm
            else
            {
                return (false);
            }

            // Get the width...
            iIndex = GetIndex(abPdfRaster, 0, 512, "/Width ");
            if (iIndex < 0) return (false);
            iIndex += 7;
            iCount = GetIndex(abPdfRaster, iIndex, 512, " ");
            if (iCount < 0) return (false);
            iCount -= iIndex;
            a_lWidth = long.Parse(Encoding.UTF8.GetString(abPdfRaster, iIndex, iCount));

            // Get the height...
            iIndex = GetIndex(abPdfRaster, 0, 512, "/Height ");
            if (iIndex < 0) return (false);
            iIndex += 8;
            iCount = GetIndex(abPdfRaster, iIndex, 512, " ");
            if (iCount < 0) return (false);
            iCount -= iIndex;
            a_lHeight = long.Parse(Encoding.UTF8.GetString(abPdfRaster, iIndex, iCount));

            // Get the resolution.  The MediaBox has the values in terms of 72dpi, so we
            // get the original resolution by dividing that value to get the height in
            // inches, and then dividing the pixel height by that amount.  Since the pixel
            // height was calculated using the original resolution, this yields the value
            // that we want...
            iIndex = GetIndex(abPdfRaster, 0, 0, "/MediaBox [");
            if (iIndex < 0) return (false);
            iIndex += 11;
            iCount = GetIndex(abPdfRaster, iIndex, 0, "]");
            if (iCount < 0) return (false);
            iCount -= iIndex;
            string[] aszMediaBox = Encoding.UTF8.GetString(abPdfRaster, iIndex, iCount).Split(new char[] { ' ', ']' }, StringSplitOptions.RemoveEmptyEntries);
            double dfHeight = double.Parse(aszMediaBox[3]) / 72.0;
            a_lResolution = (long)((double)a_lHeight / dfHeight);

            // Get the image information, the index of the first byte and the total bytes...
            iIndex = GetIndex(abPdfRaster, 0, 512, ">>stream\r\n");
            if (iIndex < 0) return (false);
            iIndex += 10;
            iCount = GetIndex(abPdfRaster, iIndex, 0, "\r\nendstream\r\n\nendobj\n");
            if (iCount < 0) return (false);
            iCount -= iIndex;

            // Add a header, if needed...
            switch (a_rasterpixelformat)
            {
                default:
                    return (false);
                    
                case PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL:
                    switch (a_rastercompression)
                    {
                        default:
                            return (false);
                            
                        case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED:

                            tiffbitonaluncompressed = new TiffBitonalUncompressed((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffbitonaluncompressed);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffbitonaluncompressed, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                            
                        case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_CCITTG4:
                            tiffbitonalg4 = new TiffBitonalG4((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffbitonalg4);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffbitonalg4, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                    }
                    break;
                    
                case PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_GRAYSCALE:
                    switch (a_rastercompression)
                    {
                        default:
                            return (false);
                            
                        case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED:
                            tiffgrayscaleuncompressed = new TiffGrayscaleUncompressed((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffgrayscaleuncompressed);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffgrayscaleuncompressed, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                            
                        case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_JPEG:
                            iHeader = 0;
                            a_abImage = new byte[iCount];
                            break;
                    }
                    break;
                    
                case PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_RGB:
                    switch (a_rastercompression)
                    {
                        default:
                            return (false);
                            
                        case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED:
                            tiffcoloruncompressed = new TiffColorUncompressed((uint)a_lWidth, (uint)a_lHeight, (uint)a_lResolution, (uint)iCount);
                            iHeader = Marshal.SizeOf(tiffcoloruncompressed);
                            a_abImage = new byte[iHeader + iCount];
                            intptr = Marshal.AllocHGlobal(iHeader);
                            Marshal.StructureToPtr(tiffcoloruncompressed, intptr, true);
                            Marshal.Copy(intptr, a_abImage, 0, iHeader);
                            Marshal.FreeHGlobal(intptr);
                            break;
                            
                        case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_JPEG:
                            iHeader = 0;
                            a_abImage = new byte[iCount];
                            break;
                    }
                    break;
            }
            
            // Copy the image data...
            Buffer.BlockCopy(abPdfRaster, iIndex, a_abImage, iHeader, iCount);

            // All done...
            return (true);
        }

        /// <summary>
        /// Return the index of the first byte of a pattern, if found
        /// in a byte array of data...
        /// </summary>
        /// <param name="a_abData">data to search</param>
        /// <param name="a_iOffset">byte offset to start searching</param>
        /// <param name="a_iLength">length to try, 0 for max</param>
        /// <param name="a_abPattern">pattern to find</param>
        /// <returns>index of first byte or -1</returns>
        private static int GetIndex(byte[] a_abData, int a_iOffset, int a_iLength, string a_szPattern)
        {
            int dd;
            int oo;
            int pp;

            // Validate...
            if ((a_abData == null) || (a_szPattern == null) || (a_szPattern.Length > a_abData.Length))
            {
                return (-1);
            }

            // Modify the length, if needed...
            if ((a_iLength == 0) || (a_iLength > a_abData.Length))
            {
                a_iLength = a_abData.Length;
            }

            // More validating...
            if (a_szPattern.Length > a_iLength)
            {
                return (-1);
            }

            // Move byte-by-byte through the data...
            for (dd = a_iOffset; dd < a_iLength; dd++)
            {
                oo = dd;

                // Look for the pattern...
                for (pp = 0; (oo < a_iLength) && (pp < a_szPattern.Length); oo++, pp++)
                {
                    if (a_abData[oo] != a_szPattern[pp])
                    {
                        break;
                    }
                }

                // Found it, return the index......
                if (pp >= a_szPattern.Length)
                {
                    return (oo - a_szPattern.Length);
                }
            }

            // No joy...
            return (-1);
        }

        /// <summary>
        /// create and return a raster PDF encoder.
        /// apiLevel is the version of this API that the caller is expecting.
        /// (You can use PDFRAS_API_LEVEL)
        /// os points to a structure containing various functions and
        /// handles provided by the caller to the raster encoder.
        /// </summary>
        /// <param name="apiLevel"></param>
        /// <returns></returns>
        public object pd_raster_encoder_create(int apiLevel, t_OS os)
        {
	        t_pdallocsys pool = new t_pdallocsys();

	        if (apiLevel != 1)
            {
		        // TODO: report invalid API level
		        return (null);
	        }

	        t_pdfrasencoder enc = new t_pdfrasencoder();
	        if (enc != null)
	        {
		        enc.pool = pool;
		        enc.stm = pd_outstream_new(pool, os);
		        // fill in various defaults
		        enc.keywords = null;
		        enc.creator = null;
                enc.producer = "PdfRaster encoder " + PdfRasterWriter.Writer.PdfRasterConst.PDFRASWR_LIBRARY_VERSION;
                // default sample precision
                enc.bitsPerChannel = 8;
		        // default resolution
		        enc.xdpi = enc.ydpi = 42;

		        enc.xref = pd_xref_new(pool);
		        enc.catalog = pd_catalog_new(pool, enc.xref);
		        // Write the PDF header, with the PDF/raster 2nd line comment:
		        pd_write_pdf_header(enc.stm, "1.4", "%\xAE\xE2\x9A\x86" + "er-1.0");
	        }
	        return (enc);
        }

        /// <summary>
        /// Set the 'creator' document property.
        /// Customarily set to the name and version of the creating application.
        /// </summary>
        /// <param name="a_enc"></param>
        /// <param name="creator"></param>
        public static void pd_raster_set_creator(object a_enc, string creator)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
	        enc.creator = creator;
        }

        /// <summary>
        /// Set the resolution for subsequent pages
        /// </summary>
        /// <param name="a_enc"></param>
        /// <param name="xdpi"></param>
        /// <param name="ydpi"></param>
        public static void pd_raster_set_resolution(object a_enc, double xdpi, double ydpi)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
	        enc.xdpi = xdpi;
	        enc.ydpi = ydpi;
        }

        /// <summary>
        /// Start encoding a page in the current document.
        /// If a page is currently open, it is automatically ended before the new page is started.
        /// </summary>
        /// <param name="a_enc"></param>
        /// <param name="format"></param>
        /// <param name="comp"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        public int pd_raster_encoder_start_page(object a_enc, PdfRasterWriter.Writer.PdfRasterPixelFormat format, PdfRasterWriter.Writer.PdfRasterCompression comp, int width)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
	        if (IS_DICT(enc.currentPage))
            {
		        pd_raster_encoder_end_page(enc);
		        //assert(IS_NULL(enc.currentPage));
	        }
	        enc.pixelFormat = format;
	        enc.compression = comp;
	        enc.width = width;
	        enc.height = 0;			// unknown until strips are written
	        double W = (width / enc.xdpi) * 72.0;
	        // Start a new page (of unknown height)
	        enc.currentPage = pd_page_new_simple(enc.pool, enc.xref, enc.catalog, W, 0);
	        //assert(IS_REFERENCE(enc.currentPage));

	        return (0);
        }

        /// <summary>
        /// Append a strip h rows high to the current page of the current document.
        /// The data is len bytes, starting at buf.
        /// The data must be in the correct pixel format, must have the width given for the page,
        /// and must be compressed with the specified compression.
        /// Can be called any number of times to deliver the data for the current page.
        /// Invalid if no page is open.
        /// The data is copied byte - for - byte into the output PDF.
        /// Each row must start on the next byte following the last byte of the preceding row.
        /// JPEG compressed data must be encoded in the JPEG baseline format.
        /// Color images must be transformed to YUV space as part of JPEG compression, grayscale images are not transformed.
        /// CCITT compressed data must be compressed in accordance with the following PDF Optional parameters for the CCITTFaxDecode filter:
        /// K = -1, EndOfLine=false, EncodedByteAlign=false, BlackIs1=false
        /// </summary>
        /// <param name="enc"></param>
        /// <param name="rows"></param>
        /// <param name="buf"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        public int pd_raster_encoder_write_strip(object a_enc, int rows, byte[] buf, long offset, long len)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
            e_ColorSpace colorspace = (enc.pixelFormat == PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_RGB) ? e_ColorSpace.kDeviceRgb : e_ColorSpace.kDeviceGray;
            e_ImageCompression comp;
	        switch (enc.compression)
            {
                case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_CCITTG4:
                    comp = e_ImageCompression.kCompCCITT;
		            break;
                case PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_JPEG:
                    comp = e_ImageCompression.kCompDCT;
		            break;
	            default:
                    comp = e_ImageCompression.kCompNone;
		            break;
            }
            int bitsPerComponent = (enc.pixelFormat == PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL) ? 1 : enc.bitsPerChannel;
            t_stripinfo stripinfo = new t_stripinfo(buf, (uint)offset, (uint)len);
	        t_pdvalue image = pd_image_new_simple
            (
                enc.pool,
                enc.xref,
                onimagedataready,
                stripinfo,
		        (UInt32)enc.width,
                (UInt32)rows,
                (UInt32)bitsPerComponent,
		        comp,
		        e_CCITTKind.kCCIITTG4, false,			// ignored unless compression is CCITT
		        colorspace
            );
	        // get a reference to this (strip) image
	        t_pdvalue imageref = pd_xref_makereference(enc.xref, image);
	        // add the image to the resources of the current page
	        pd_page_add_image(enc.currentPage, PdfStandardAtoms.PDA_STRIP0, imageref);
	        // flush the image stream
	        pd_write_pdreference_declaration(enc.stm, imageref.value.refvalue);
	        enc.height += rows;

	        return (0);
        }

        /// <summary>
        /// Finish writing the current page to the current document.
        /// Invalid if no page is open.
        /// After this call succeeds, no page is open.
        /// </summary>
        /// <param name="a_enc"></param>
        /// <returns></returns>
        public int pd_raster_encoder_end_page(object a_enc)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
	        if (!IS_NULL(enc.currentPage))
            {
		        // create a content generator
		        t_pdcontents_gen gen = pd_contents_gen_new(enc.pool, content_generator, enc);
		        // create contents object (stream)
		        t_pdvalue contents = pd_xref_makereference(enc.xref, pd_contents_new(enc.pool, enc.xref, gen));
		        // flush (write) the contents stream
		        pd_write_pdreference_declaration(enc.stm, contents.value.refvalue);
		        // add the contents to the current page
		        dict_put(enc.currentPage, PdfStandardAtoms.PDA_CONTENTS, contents);
		        // update the media box (we didn't really know the height until now)
		        update_media_box(enc, enc.currentPage);
		        // flush (write) the current page
		        pd_write_pdreference_declaration(enc.stm, enc.currentPage.value.refvalue);
		        // add the current page to the catalog (page tree)
		        pd_catalog_add_page(enc.catalog, enc.currentPage);
		        // done with current page:
		        enc.currentPage = pdnullvalue();
	        }
	        return 0;
        }

        /// <summary>
        /// End the current PDF, finish writing all data to the output.
        /// </summary>
        /// <param name="a_enc"></param>
        public void pd_raster_encoder_end_document(object a_enc)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
	        pd_raster_encoder_end_page(enc);
	        t_pdvalue info = pd_info_new(enc.pool, enc.xref, "image", "TWAIN Direct", "image", enc.keywords, enc.creator, enc.producer);
	        pd_write_endofdocument(enc.pool, enc.stm, enc.xref, enc.catalog, info);
	        pd_xref_free(ref enc.xref);
        }

        /// <summary>
        /// Destroy a raster PDF encoder, releasing all associated resources.
        /// Do not use the enc pointer after this, it is invalid.
        /// </summary>
        /// <param name="a_enc"></param>
        public void pd_raster_encoder_destroy(object a_enc)
        {
            t_pdfrasencoder enc = (t_pdfrasencoder)a_enc;
	        if (enc != null)
            {
		        t_pdallocsys pool = enc.pool;
		        pd_alloc_sys_free(ref pool);
	        }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Public Definitions: PdfRaster
        ///////////////////////////////////////////////////////////////////////////////
        #region Public Definitions: PdfRaster
        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Methods: PdfRaster
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Methods: PdfRaster

        private void onimagedataready(t_datasink sink, object eventcookie)
        {
	        t_stripinfo pinfo = (t_stripinfo)eventcookie;
	        pd_datasink_begin(sink);
	        pd_datasink_put(sink, pinfo.data, pinfo.offset, pinfo.count);
	        pd_datasink_end(sink);
	        pd_datasink_free(ref sink);
        }

        // callback to generate the content text for a page.
        // it draws the strips of the page in order from top to bottom.
        private void content_generator(t_pdcontents_gen gen, object cookie)
        {
	        t_pdfrasencoder enc = (t_pdfrasencoder)cookie;
	        // compute width & height of page in PDF points
	        double W = enc.width / enc.xdpi * 72.0;
	        double H = enc.height / enc.ydpi * 72.0;
	        // horizontal (x) offset is always 0 - flush to left edge.
	        double tx = 0;
	        // vertical offset starts at 0? Top of page?
	        // TODO: ty needs to be stepped for each strip
	        double ty = 0;
	        // TODO: draw all strips not just the first
	        pd_gen_gsave(gen);
	        pd_gen_concatmatrix(gen, W, 0, 0, H, tx, ty);
	        pd_gen_xobject(gen, PdfStandardAtoms.PDA_STRIP0);
	        pd_gen_grestore(gen);
        }

        private void update_media_box(t_pdfrasencoder enc, t_pdvalue page)
        {
	        bool success = false;
	        t_pdvalue box = dict_get(page, PdfStandardAtoms.PDA_MEDIABOX, ref success);
	        //assert(success);
	        //assert(IS_ARRAY(box));
	        double W = enc.width / enc.xdpi * 72.0;
	        double H = enc.height / enc.ydpi * 72.0;
	        pd_array_set(box.value.arrvalue, 2, pdfloatvalue(W));
	        pd_array_set(box.value.arrvalue, 3, pdfloatvalue(H));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions: PdfRaster
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions: PdfRaster

        private class t_pdfrasencoder
        {
            public t_pdfrasencoder()
            {
                pool = null;
                stm = null;
                writercookie = null;
                keywords = null;
                creator = null;
                producer = null;
                xref = null;
                catalog = null;
                xdpi = 0;
                ydpi = 0;
                bitsPerChannel = 0;
                currentPage = new t_pdvalue();
                pixelFormat = PdfRasterWriter.Writer.PdfRasterPixelFormat.PDFRASWR_BITONAL;
                width = 0;
                compression = PdfRasterWriter.Writer.PdfRasterCompression.PDFRASWR_UNCOMPRESSED;
                height = 0;
            }

            public t_pdallocsys pool;
            public t_pdoutstream stm;				// output PDF stream
            public object writercookie;
            // document info
            public string keywords;
            public string creator;			        // name of application creating the PDF
            public string producer;			        // name of PDF writing module
            // internal PDF structures
            public t_pdxref xref;
            public t_pdvalue catalog;
            // current page object and related values
            public double xdpi;				        // horizontal resolution, pixels/inch
            public double ydpi;				        // vertical resolution, pixels/inch
            public int bitsPerChannel;		        // 1 for bitonal, 8 otherwise
            public t_pdvalue currentPage;
            public PdfRasterWriter.Writer.PdfRasterPixelFormat pixelFormat;	// how pixels are represented/
            public int width;				        // image width in pixels
            public PdfRasterWriter.Writer.PdfRasterCompression compression;	// how data is compressed
            public int height;				        // total pixel height of current page
        }

        private class t_stripinfo
        {
            public t_stripinfo(byte[] a_data, UInt32 a_offset, UInt32 a_count)
            {
                data = a_data;
                offset = a_offset;
                count = a_count;
            }

	        public byte[] data;
            public UInt32 offset;
            public UInt32 count;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfAlloc
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfAlloc

        /*
        private byte[] pd_alloc(t_pdallocsys allocsys, int bytes) { return (__pd_alloc(allocsys, bytes, null)); }
        private void pd_free(t_pdallocsys allocsys, ref byte[] ptr) { __pd_free(allocsys, ref ptr, false); }
        */ 

        public class t_heapelem
        {
            public t_heapelem()
            {
	            prev = null;
	            next = null;
	            size = 0;
	            allocnumber = 0;
                #if PDDEBUG
	                allocator = null;
                #endif
                data = null;
            }

	        public t_heapelem prev;
	        public t_heapelem next;
	        public int size;
	        public long allocnumber;
            #if PDDEBUG
	            public string allocator;
            #endif
	        public byte[] data;
        }

        public class t_pdallocsys
        {
	        public t_OS os;
	        public t_heapelem heap;
	        public long allocnumber;
        }

        public static t_pdallocsys pd_alloc_sys_new(t_OS os)
        {
	        t_pdallocsys allocsys = null; 
	        if (os == null) return (null);

            allocsys = new t_pdallocsys();
	        if (allocsys == null) return (null);
	        allocsys.os = os;
	        allocsys.allocnumber = 0;
	        allocsys.heap = null;
	        return (allocsys);
        }

        /*
        private byte[] __pd_alloc(t_pdallocsys allocsys, int bytes, string allocatedBy)
        {
	        t_heapelem elem = null;
	        int totalBytes = 0;

	        if (allocsys == null) return (null);
	        totalBytes = bytes + Marshal.SizeOf(elem);
            elem = new t_heapelem();
            elem.data = new byte[bytes];
	        if (elem == null) return (null);

	        elem.prev = allocsys.heap;
	        elem.next = null;
	        allocsys.heap = elem;
	        if (elem.prev != null)
            {
		        elem.prev.next = elem;
            }

            #if PDDEBUG
	            elem.allocator = allocatedBy;
            #endif

	        allocsys.os.memclear(elem.data, bytes);
	        elem.size = bytes;
	        return (elem.data);
        }

        private void __pd_free(t_pdallocsys allocsys, ref byte[] ptr, bool validate)
        {
	        t_heapelem elem;
	        int offset = offsetof(t_heapelem, data);
	        if (ptr == null) return;
	        elem = (t_heapelem *)(((pduint8 *)ptr) - offset);

            #if PDDEBUG
	            if (validate)
	            {
		            t_heapelem walker = allocsys.heap;
		            while (walker)
		            {
			            if (walker.data == ptr) break;
			            walker = walker.prev;
		            }
		            assert(walker);
		            assert(elem == walker);
	            }
            #endif

	        if (elem == allocsys.heap)
	        {
		        allocsys.heap = elem.prev;
		        if (elem.prev != null) elem.prev.next = null;
	        }
	        else
	        {
		        elem.next.prev = elem.prev;
		        if (elem.prev != null) elem.prev.next = elem.next;
		        allocsys.os.memclear(elem, Marshal.SizeOf(elem) + elem.size);
	        }
	        allocsys.os.free(ref elem);
        }
        */

        private static void pd_alloc_sys_free(ref t_pdallocsys sys)
        {
	        sys = null;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfArray
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfArray

        private delegate bool f_pdarray_iterator(t_pdarray arr, UInt32 currindex, t_pdvalue value, object cookie);

        private class PdfArrayConst
        {
            public const int kSomeReasonableDefaultSize = 12;
        }
        
        private class t_pdarray
        {
	        public t_pdallocsys alloc;
	        public UInt32 size;
	        public UInt32 maxsize;
	        public t_pdvalue[] data;
        }

        private t_pdarray pd_array_new(t_pdallocsys alloc, UInt32 initialSize)
        {
	        t_pdarray arr;
	        if (alloc == null) return (null);
	        initialSize = (initialSize != 0) ? initialSize : PdfArrayConst.kSomeReasonableDefaultSize;
	        arr = new t_pdarray();
	        if (arr == null) return (null);
	        arr.alloc = alloc;
	        arr.size = 0;
	        arr.maxsize = initialSize;
	        arr.data = new t_pdvalue[initialSize];
	        return (arr);
        }

        private static void pd_array_free(ref t_pdarray arr)
        {
	        if (arr == null) return;
	        if (arr.data != null)
	        {
		        arr.data = null;
	        }
	        arr = null;
        }

        private UInt32 pd_array_size(t_pdarray arr)
        {
	        if (arr == null) return (0);
	        return (arr.size);
        }

        private UInt32 pd_array_maxsize(t_pdarray arr)
        {
	        if (arr == null) return (0);
	        return (arr.maxsize);
        }

        private t_pdvalue pd_array_get(t_pdarray arr, UInt32 index)
        {
	        if ((arr == null) || (index >= arr.size)) return (pderrvalue());
	        return (arr.data[index]);
        }

        private void pd_array_set(t_pdarray arr, UInt32 index, t_pdvalue value)
        {
	        if ((arr == null) || (index >= arr.size)) return;
	        arr.data[index] = value;
        }

        private void grow_by_one(t_pdarray arr)
        {
	        if (arr.size == arr.maxsize)
	        {
		        t_pdvalue[] olddata = arr.data;
		        UInt32 i;
		        arr.maxsize = arr.maxsize * 5 / 2; /* reasonable? */
		        arr.data = new t_pdvalue[arr.maxsize];
		        if (arr.data == null)
		        {
			        /* TODO - FAIL */
			        return;
		        }
		        for (i = 0; i < arr.size; i++)
		        {
			        arr.data[i] = olddata[i];
		        }
		        olddata = null;
	        }
	        arr.size++;
        }

        private void copy_elements(t_pdarray arr, UInt32 from, UInt32 size, UInt32 to)
        {
	        UInt32 i;
	        if (to == from) return;
	        if (to < from)
	        {
		        for (i = 0; i < (int)size; i++)
		        {
			        arr.data[to + i] = arr.data[from + i];
		        }
	        }
	        else {
		        for (i = size - 1; i >= 0; i++)
		        {
			        arr.data[to + i] = arr.data[from + i];
		        }
	        }
        }

        private void pd_array_insert(t_pdarray arr, UInt32 index, t_pdvalue value)
        {
	        if (arr == null) return;
	        grow_by_one(arr);
	        copy_elements(arr, index, arr.size - index - 1, index + 1);
	        arr.data[index] = value;
        }

        private void pd_array_add(t_pdarray arr, t_pdvalue value)
        {
	        if (arr == null) return;
	        grow_by_one(arr);
	        arr.data[arr.size - 1] = value;
        }

        private t_pdvalue pd_array_remove(t_pdarray arr, UInt32 index)
        {
	        t_pdvalue val;
	        if ((arr == null) || (index >= arr.size)) return (pderrvalue());
	        val = arr.data[index];
	        copy_elements(arr, index + 1, arr.size - index - 1, index);
	        arr.size--;
	        return (val);
        }

        private static void pd_array_foreach(t_pdarray arr, f_pdarray_iterator iter, object cookie)
        {
	        UInt32 i;
	        if ((arr == null) || (iter == null) || (arr.size == 0)) return;
	        for (i = 0; i < arr.size; i++)
	        {
		        if (!iter(arr, i, arr.data[i], cookie))
			        break;
	        }
        }

        private t_pdarray pd_array_build(t_pdallocsys alloc, UInt32 size, t_pdvalue value, t_pdvalue[] tpdvalue)
        {
	        UInt32 i;
	        t_pdarray arr = pd_array_new(alloc, size);
	        if (arr == null) return (null);
	        for (i = 0; i < size; i++)
	        {
		        pd_array_add(arr, tpdvalue[i]);
	        }
	        return (arr);
        }

        private t_pdarray pd_array_buildints(t_pdallocsys alloc, UInt32 size, Int32[] ai)
        {
	        UInt32 i;
	        t_pdarray arr = pd_array_new(alloc, size);
	        if (arr == null) return (null);
	        for (i = 0; i < size; i++)
	        {
		        pd_array_add(arr, pdintvalue(ai[i]));
	        }
	        return (arr);
        }

        private t_pdarray pd_array_buildfloats(t_pdallocsys alloc, UInt32 size, double[] ad)
        {
	        UInt32 i;
	        t_pdarray arr = pd_array_new(alloc, size);
	        if (arr == null) return (null);
	        for (i = 0; i < size; i++)
	        {
		        pd_array_add(arr, pdfloatvalue(ad[i]));
	        }
	        return arr;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfAtoms
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfAtoms

        private class t_atomsys
        {
            public t_atomsys()
            {
                os = null;
            }

	        public t_OS os;
        }

        private class t_atomelem
        {
            public t_atomelem(string a_strName, PdfStandardAtoms a_atom)
            {
                strName = a_strName;
                atom = a_atom;
            }
	        public string strName;
	        public PdfStandardAtoms atom;
        }

        private static t_atomelem[] __atomstrs = null;

        private static string pd_string_from_atom(UInt32 atom)
        {
            if (__atomstrs == null)
            {
                __atomstrs = new t_atomelem[49];
	            /* 0 */
	            __atomstrs[0] = new t_atomelem(null, PdfStandardAtoms.PDA_UNDEFINED_ATOM);
                __atomstrs[1] = new t_atomelem("Type", PdfStandardAtoms.PDA_TYPE);
	            __atomstrs[2] = new t_atomelem("Pages", PdfStandardAtoms.PDA_PAGES);
	            __atomstrs[3] = new t_atomelem("Size", PdfStandardAtoms.PDA_SIZE);
	            __atomstrs[4] = new t_atomelem("Root", PdfStandardAtoms.PDA_ROOT);
	            __atomstrs[5] = new t_atomelem("Info", PdfStandardAtoms.PDA_INFO);
	            __atomstrs[6] = new t_atomelem("ID", PdfStandardAtoms.PDA_ID);
	            __atomstrs[7] = new t_atomelem("Catalog", PdfStandardAtoms.PDA_CATALOG);
	            __atomstrs[8] = new t_atomelem("Parent", PdfStandardAtoms.PDA_PARENT);
	            __atomstrs[9] = new t_atomelem("Kids", PdfStandardAtoms.PDA_KIDS);
	            /* 10 */
	            __atomstrs[10] = new t_atomelem("Count", PdfStandardAtoms.PDA_COUNT);
	            __atomstrs[11] = new t_atomelem("Page", PdfStandardAtoms.PDA_PAGE);
	            __atomstrs[12] = new t_atomelem("Resources", PdfStandardAtoms.PDA_RESOURCES);
	            __atomstrs[13] = new t_atomelem("MediaBox", PdfStandardAtoms.PDA_MEDIABOX);
	            __atomstrs[14] = new t_atomelem("CropBox", PdfStandardAtoms.PDA_CROPBOX);
	            __atomstrs[15] = new t_atomelem("Contents", PdfStandardAtoms.PDA_CONTENTS);
	            __atomstrs[16] = new t_atomelem("Rotate", PdfStandardAtoms.PDA_ROTATE);
	            __atomstrs[17] = new t_atomelem("Length", PdfStandardAtoms.PDA_LENGTH);
	            __atomstrs[18] = new t_atomelem("Filter", PdfStandardAtoms.PDA_FILTER);
	            __atomstrs[19] = new t_atomelem("DecodeParms", PdfStandardAtoms.PDA_DECODEPARMS);
	            /* 20 */
	            __atomstrs[20] = new t_atomelem("Subtype", PdfStandardAtoms.PDA_SUBTYPE);
	            __atomstrs[21] = new t_atomelem("Width", PdfStandardAtoms.PDA_WIDTH);
	            __atomstrs[22] = new t_atomelem("Height", PdfStandardAtoms.PDA_HEIGHT);
	            __atomstrs[23] = new t_atomelem("BitsPerComponent", PdfStandardAtoms.PDA_BITSPERCOMPONENT);
	            __atomstrs[24] = new t_atomelem("ColorSpace", PdfStandardAtoms.PDA_COLORSPACE);
	            __atomstrs[25] = new t_atomelem("Image", PdfStandardAtoms.PDA_IMAGE);
	            __atomstrs[26] = new t_atomelem("XObject", PdfStandardAtoms.PDA_XOBJECT);
	            __atomstrs[27] = new t_atomelem("Title", PdfStandardAtoms.PDA_TITLE);
	            __atomstrs[28] = new t_atomelem("Subject", PdfStandardAtoms.PDA_SUBJECT);
	            __atomstrs[29] = new t_atomelem("Author", PdfStandardAtoms.PDA_AUTHOR);
	            /* 30 */
	            __atomstrs[30] = new t_atomelem("Keywords", PdfStandardAtoms.PDA_KEYWORDS);
	            __atomstrs[31] = new t_atomelem("Creator", PdfStandardAtoms.PDA_CREATOR);
	            __atomstrs[32] = new t_atomelem("Producer", PdfStandardAtoms.PDA_PRODUCER);
	            __atomstrs[33] = new t_atomelem("None", PdfStandardAtoms.PDA_NONE);
	            __atomstrs[34] = new t_atomelem("FlateDecode", PdfStandardAtoms.PDA_FLATEDECODE);
	            __atomstrs[35] = new t_atomelem("CCITTFaxDecode", PdfStandardAtoms.PDA_CCITTFAXDECODE);
	            __atomstrs[36] = new t_atomelem("DCTDecode", PdfStandardAtoms.PDA_DCTDECODE);
	            __atomstrs[37] = new t_atomelem("JBIG2Decode", PdfStandardAtoms.PDA_JBIG2DECODE);
	            __atomstrs[38] = new t_atomelem("JPXDecode", PdfStandardAtoms.PDA_JPXDECODE);
	            __atomstrs[39] = new t_atomelem("K", PdfStandardAtoms.PDA_K);
	            /* 40 */
                __atomstrs[40] = new t_atomelem("Columns", PdfStandardAtoms.PDA_COLUMNS);
                __atomstrs[41] = new t_atomelem("Rows", PdfStandardAtoms.PDA_ROWS);
	            __atomstrs[42] = new t_atomelem("BlackIs1", PdfStandardAtoms.PDA_BLACKIS1);
	            __atomstrs[43] = new t_atomelem("DeviceGray", PdfStandardAtoms.PDA_DEVICEGRAY);
	            __atomstrs[44] = new t_atomelem("DeviceRGB", PdfStandardAtoms.PDA_DEVICERGB);
	            __atomstrs[45] = new t_atomelem("DeviceCMYK", PdfStandardAtoms.PDA_DEVICECMYK);
	            __atomstrs[46] = new t_atomelem("Indexed", PdfStandardAtoms.PDA_INDEXED);
	            __atomstrs[47] = new t_atomelem("ICCBased", PdfStandardAtoms.PDA_ICCBASED);
	            __atomstrs[48] = new t_atomelem("strip0", PdfStandardAtoms.PDA_STRIP0);
            }

            #if _DEBUG
	        {
		        int i;
		        for (i = 0; i < __atomstrs.Length; i++)
		        {
			        if ((int)__atomstrs[i].atom != i)
			        {
				        //assert(0);
				        // TODO - FAIL
			        }
		        }
	        }
            #endif
            return __atomstrs[atom].strName;
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfContentsGenerator
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfContentsGenerator

        private delegate void f_gen(t_pdcontents_gen gen, object gencookie);

        private class t_pdcontents_gen
        {
	        public t_pdallocsys alloc;
	        public f_gen gen;
            public object gencookie;
	        public t_datasink sink;
	        public t_pdoutstream os;
        }

        private long gen_write_out(byte[] data, long offset, long length, object cookie)
        {
            t_pdcontents_gen generator = (t_pdcontents_gen)cookie;
	        pd_datasink_put(generator.sink, data, (uint)offset, (uint)length);
	        return (length);
        }

        private t_pdcontents_gen pd_contents_gen_new(t_pdallocsys alloc, f_gen gen, object gencookie)
        {
	        t_OS opsys = new t_OS();
            t_pdcontents_gen generator = new t_pdcontents_gen();
	        if (generator == null) return (null);
	        generator.alloc = alloc;
	        generator.gen = gen;
	        generator.gencookie = gencookie;
	        opsys.writeout = gen_write_out;
	        opsys.writeoutcookie = generator;
	        generator.os = pd_outstream_new(alloc, opsys);
	        return generator;
        }

        private void pd_contents_gen_free(ref t_pdcontents_gen gen)
        {
	        if (gen == null) return;
	        pd_outstream_free(ref gen.os);
	        gen = null;
        }

        private void pd_contents_generate(t_datasink sink, object eventcookie)
        {
            t_pdcontents_gen gen = (t_pdcontents_gen)eventcookie;
	        gen.sink = sink;
	        pd_datasink_begin(sink);
	        gen.gen(gen, gen.gencookie);
	        pd_datasink_end(sink);
	        pd_datasink_free(ref sink);
        }

        private void pd_gen_moveto(t_pdcontents_gen gen, double x, double y)
        {
	        pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, x);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, y);
	        pd_puts(gen.os, " m");
        }

        private void pd_gen_lineto(t_pdcontents_gen gen, double x, double y)
        {
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, x);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, y);
	        pd_puts(gen.os, " l");
        }

        private void pd_gen_closepath(t_pdcontents_gen gen)
        {
	        pd_puts(gen.os, " h");
        }

        private void pd_gen_stroke(t_pdcontents_gen gen)
        {
	        pd_puts(gen.os, " S");
        }

        private void pd_gen_fill(t_pdcontents_gen gen, bool evenodd)
        {
	        pd_puts(gen.os, (evenodd ? " f*" : " f"));
        }

        private void pd_gen_gsave(t_pdcontents_gen gen)
        {
	        pd_puts(gen.os, " q");
        }

        private void pd_gen_grestore(t_pdcontents_gen gen)
        {
	        pd_puts(gen.os, " Q");
        }

        private void pd_gen_concatmatrix(t_pdcontents_gen gen, double a, double b, double c, double d, double e, double f)
        {
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, a);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, b);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, c);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, d);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, e);
            pd_putc(gen.os, (byte)' ');
	        pd_putfloat(gen.os, f);
	        pd_puts(gen.os, " cm");
        }

        private void pd_gen_xobject(t_pdcontents_gen gen, PdfStandardAtoms xobjectatom)
        {
            pd_putc(gen.os, (byte)' ');
	        pd_write_value(gen.os, pdatomvalue(xobjectatom));
	        pd_puts(gen.os, " Do");
        }
        
        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfDatasink
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfDatasink

        private delegate void f_sink_begin(object cookie);
        private delegate bool f_sink_put(byte[] buffer, UInt32 offset, UInt32 len, object cookie);
        private delegate void f_sink_end(object cookie);
        private delegate void f_sink_free(ref object cookie);

        private class t_datasink
        {
            public t_datasink()
            {
	            alloc = null;
                begin = null;
                put = null;
                end = null;
                free = null;
                cookie = null;
            }

	        public t_pdallocsys alloc;
	        public f_sink_begin begin;
	        public f_sink_put put;
	        public f_sink_end end;
	        public f_sink_free free;
            public object cookie;
        }

        private t_datasink pd_datasink_new(t_pdallocsys alloc, f_sink_begin begin, f_sink_put put, f_sink_end end, f_sink_free free, object cookie)
        {
	        t_datasink sink = null;
	
	        if ((begin == null) || (end == null) || (put == null) || (free == null)) return (null);

	        sink = new t_datasink();
	        if (sink == null) return (null);
	        sink.begin = begin;
	        sink.put = put;
	        sink.end = end;
	        sink.free = free;
	        sink.cookie = cookie;
	        return (sink);
        }

        private void pd_datasink_free(ref t_datasink sink)
        {
	        if (sink == null) return;
	        sink.free(ref sink.cookie);
        }

        private void pd_datasink_begin(t_datasink sink)
        {
	        if (sink == null) return;
	        sink.begin(sink.cookie);
        }

        private bool pd_datasink_put(t_datasink sink, byte[] data, UInt32 offset, UInt32 len)
        {
	        if ((sink == null) || (data == null)) return (false);
	        return (sink.put(data, offset, len, sink.cookie));
        }

        private void pd_datasink_end(t_datasink sink)
        {
            if (sink == null) return;
	        sink.end(sink.cookie);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfDict
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfDict

        private delegate void f_on_datasink_ready(t_datasink sink, object eventcookie);

        private class t_pddict
        {
            public t_pddict()
            {
                alloc = null;
	            isStream = false;
	            elems = null;
                cookie = null;
            }

	        public t_pdallocsys alloc;
	        public bool isStream;
	        public t_pdhashatomtovalue elems;
            public object cookie;
        }

        private class t_pdstream
        {
            public t_pdstream()
            {
                dict = new t_pddict();
	            sink = new t_datasink();
	            ready = null;
                readycookie = null;
            }

	        public t_pddict dict; /* must be first */
	        public t_datasink sink;
	        public f_on_datasink_ready ready;
	        public object readycookie;
        }

        private t_pdvalue dict_new(t_pdallocsys allocsys, int initialsize)
        {
	        t_pdhashatomtovalue hash;
	        t_pddict dict;
	        t_pdvalue dictvalue;
	        hash = pd_hashatomtovalue_new(allocsys, initialsize);
	        if (hash == null) return pdnullvalue();

	        dict = new t_pddict();
	        if (dict == null) return pdnullvalue();
	        dict.alloc = allocsys;
	        dict.elems = hash;
	        dict.isStream = false;
            dictvalue = new t_pdvalue();
	        dictvalue.pdtype = t_pdtype.TPDDICT;
	        dictvalue.value.dictvalue = dict;
	        return (dictvalue);
        }

        private void dict_free(t_pdvalue dict)
        {
	        t_pdallocsys alloc;
	        if (!IS_DICT(dict)) return;
	        if (dict.value.dictvalue == null) return;
	        alloc = pd_hashatomtovalue_getallocsys(dict.value.dictvalue.elems);
	        /* this does not free the elements individually.
	         * yes, this will leak memory.
	         * no, this won't ultimately be an issue.
	         * the object may be referenced in multiple places so
	         * this is not our job.
	         */
	        pd_hashatomtovalue_free(ref dict.value.dictvalue.elems);
	        dict.value.dictvalue = null;
        }

        private t_pdvalue dict_get(t_pdvalue dict, PdfStandardAtoms key, ref bool success)
        {
	        if (IS_REFERENCE(dict)) dict = pd_reference_get_value(dict.value.refvalue);
	        if (!IS_DICT(dict) || (dict.value.dictvalue == null)) { success = false; return (pdnullvalue()); }
	        return (pd_hashatomtovalue_get(dict.value.dictvalue.elems, key, ref success));
        }

        private t_pdvalue dict_put(t_pdvalue dict, PdfStandardAtoms key, t_pdvalue value)
        {
	        if (IS_REFERENCE(dict)) dict = pd_reference_get_value(dict.value.refvalue);
	        if (!IS_DICT(dict) || (dict.value.dictvalue == null)) return pderrvalue();
	        pd_hashatomtovalue_put(dict.value.dictvalue.elems, key, value);
	        return (dict);
        }

        private bool dict_is_stream(t_pdvalue dict)
        {
	        if (IS_REFERENCE(dict)) dict = pd_reference_get_value(dict.value.refvalue);
	        if (!IS_DICT(dict)) return (false);
	        return (dict.value.dictvalue.isStream);
        }


        private void dict_foreach(t_pdvalue dict, f_pdhashatomtovalue_iterator iter, object cookie)
        {
	        if (IS_REFERENCE(dict)) dict = pd_reference_get_value(dict.value.refvalue);
	        if (!IS_DICT(dict) || (dict.value.dictvalue == null)) return;
	        pd_hashatomtovalue_foreach(dict.value.dictvalue.elems, iter, cookie);
        }

        private class t_pdstm_sink
        {
	        public t_pdallocsys alloc;
	        public t_pdstream stm;
	        public t_pdreference lengthref;
	        public t_pdoutstream outstm;
	        public UInt32 startpos;
        }

        private t_pdstm_sink pd_stm_sink_new(t_pdallocsys alloc, t_pdstream stm, t_pdoutstream outstm)
        {
	        bool succ = false;
            t_pdstm_sink sink = new t_pdstm_sink();
	        t_pdreference lengthref = pd_hashatomtovalue_get(stm.dict.elems, PdfStandardAtoms.PDA_LENGTH, ref succ).value.refvalue;
	        if (sink == null) return (null);
	        sink.alloc = alloc;
	        sink.stm = stm;
	        sink.lengthref = lengthref;
	        sink.outstm = outstm;
	        return (sink);
        }

        private void stm_sink_begin(object cookie)
        {
            t_pdstm_sink sink = (t_pdstm_sink)cookie;
	        pd_puts(sink.outstm, "stream\r\n");
	        sink.startpos = pd_outstream_pos(sink.outstm);
        }

        private bool stm_sink_put(byte[] buffer, UInt32 offset, UInt32 len, object cookie)
        {
            t_pdstm_sink sink = (t_pdstm_sink)cookie;
	        pd_putn(sink.outstm, buffer, offset, len);
	        return (true);
        }

        private void stm_sink_end(object cookie)
        {
            t_pdstm_sink sink = (t_pdstm_sink)cookie;
	        UInt32 finalpos = pd_outstream_pos(sink.outstm);
	        pd_puts(sink.outstm, "\r\nendstream\r\n");
	        pd_reference_set_value(sink.lengthref, pdintvalue((int)(finalpos - sink.startpos)));
        }

        private void stm_sink_free(ref object cookie)
        {
            t_pdstm_sink sink = (t_pdstm_sink)cookie;
            sink = null;
        }

        private t_datasink stream_datasink_new(t_pdallocsys allocsys, t_pdstream stm, t_pdoutstream outstm)
        {
	        t_pdstm_sink sink = pd_stm_sink_new(allocsys, stm, outstm);

            return (pd_datasink_new(allocsys, stm_sink_begin, stm_sink_put, stm_sink_end, stm_sink_free, sink));
        }

        private t_pdvalue stream_new(t_pdallocsys allocsys, t_pdxref xref, int initialsize, f_on_datasink_ready ready, object eventcookie)
        {
	        t_pdhashatomtovalue hash;
	        t_pdstream stream;
	        t_pdvalue dictvalue;
	        hash = pd_hashatomtovalue_new(allocsys, initialsize);
	        if (hash == null) return pdnullvalue();

	        stream = new t_pdstream();
	        if (stream == null) return pdnullvalue();
	        stream.dict.elems = hash;
	        stream.dict.isStream = true;
	        stream.dict.alloc = allocsys;
	        stream.ready = ready;
	        stream.readycookie = eventcookie;
            dictvalue = new t_pdvalue();
            dictvalue.pdtype = t_pdtype.TPDDICT;
	        dictvalue.value.dictvalue = stream.dict;
            dictvalue.value.dictvalue.cookie = stream;
	        return (pd_xref_makereference(xref, dictvalue));
        }

        private void stream_free(ref t_pdvalue stream)
        {
	        t_pdstream stmp = new t_pdstream();
	        if (!IS_REFERENCE(stream)) return;
	        stream = pd_reference_get_value(stream.value.refvalue);
	        if (!dict_is_stream(stream)) return;
	        stmp.dict = stream.value.dictvalue;
	        if (stmp == null) return;

            if (stmp.sink != null)
            {
                pd_datasink_free(ref stmp.sink);
            }
	        dict_free(stream);
        }

        private void stream_set_on_datasink_ready(t_pdvalue stream, f_on_datasink_ready ready, object eventcookie)
        {
            t_pdstream stmp;
	        if (!IS_REFERENCE(stream)) return;
	        stream = pd_reference_get_value(stream.value.refvalue);
	        if (!dict_is_stream(stream)) return;
            stmp = (t_pdstream)stream.value.dictvalue.cookie;
	        stmp.dict = stream.value.dictvalue;
            if (stmp.ready != null)
            {
                stmp.ready(stmp.sink, stmp.readycookie);
            }
        }

        private void stream_write_contents(t_pdvalue stream, t_pdoutstream outstm)
        {
	        t_datasink sink;
            t_pdstream stmp = new t_pdstream();
	        if (!dict_is_stream(stream)) return;
            stmp = (t_pdstream)stream.value.dictvalue.cookie;
	        stmp.dict = stream.value.dictvalue;
	        sink = stream_datasink_new(stmp.dict.alloc, stmp, outstm);
	        stmp.ready(sink, stmp.readycookie);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfElements
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfElements

        private class t_pdatom
        {
            public t_pdatom()
            {
                value = 0;
            }

            public UInt32 value;
        }

        /* OPTIONAL|REFERENCE|TYPE */

        private enum t_pdtype
        {
            TPDERRVALUE,
            TPDNULL,
            TPDNUMBERINT,
            TPDNUMBERFLOAT,
            TPDNAME,
            TPDSTRING,
            TPDARRAY,
            TPDDICT,
            TPDBOOL,
            TPDREFERENCE
        }

        private class t_pdname : t_pdatom
        {
        }

        private class u_pdvalue
        {
            public Int32 intvalue;
            public Double floatvalue;
            public UInt32 namevalue;
            public bool boolvalue;
            public t_pdstring stringvalue;
            public t_pdarray arrvalue;
            public t_pddict dictvalue;
            public t_pdreference refvalue;
        }

        private class t_pdvalue
        {
            public t_pdvalue()
            {
                isOptional = 0;
                pdtype = t_pdtype.TPDNULL;
                value = new u_pdvalue();
            }

            public int isOptional;// : 1; tbd
            public t_pdtype pdtype;// : 7; tbd
            public u_pdvalue value;
        }

        private static bool IS_ERR(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDERRVALUE); }
        private static bool IS_NULL(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDNULL); }
        private static bool IS_DICT(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDDICT); }
        private static bool IS_NUMBER(t_pdvalue v) { return ((v.pdtype == t_pdtype.TPDNUMBERINT) || (v.pdtype == t_pdtype.TPDNUMBERFLOAT)); }
        private static bool IS_INT(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDNUMBERINT); }
        private static bool IS_FLOAT(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDNUMBERFLOAT); }
        private static bool IS_STRING(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDSTRING); }
        private static bool IS_ARRAY(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDARRAY); }
        private static bool IS_BOOL(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDBOOL); }
        private static bool IS_NAME(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDNAME); }
        private static bool IS_REFERENCE(t_pdvalue v) { return (v.pdtype == t_pdtype.TPDREFERENCE); }

        private static t_pdvalue pdatomvalue(PdfStandardAtoms v)
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDNAME;
            tpdvalue.value.namevalue = (UInt32)v;
            return (tpdvalue);
        }

        private static t_pdvalue pderrvalue()
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDNULL;
            return (tpdvalue);
        }

        private static t_pdvalue pdnullvalue()
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDERRVALUE;
            return (tpdvalue);
        }

        private static t_pdvalue pdintvalue(int v)
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDNUMBERINT;
            tpdvalue.value.intvalue = v;
            return (tpdvalue);
        }

        private static t_pdvalue pdfloatvalue(double v)
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDNUMBERFLOAT;
            tpdvalue.value.floatvalue = v;
            return (tpdvalue);
        }

        private static t_pdvalue pdboolvalue(bool v)
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDBOOL;
            tpdvalue.value.boolvalue = v;
            return (tpdvalue);
        }

        private static t_pdvalue pdarrayvalue(t_pdarray arr)
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDARRAY;
            tpdvalue.value.arrvalue = arr;
            return (tpdvalue);
        }

        private static t_pdvalue pdstringvalue(t_pdstring str)
        {
            t_pdvalue tpdvalue = new t_pdvalue();
            tpdvalue.pdtype = t_pdtype.TPDSTRING;
            tpdvalue.value.stringvalue = str;
            return (tpdvalue);
        }

        private t_pdvalue pdcstrvalue(t_pdallocsys alloc, string s)
        {
            byte[] ab = Encoding.UTF8.GetBytes(s);
            t_pdstring str = pd_string_new(alloc, ab, (UInt32)ab.Length, false);
            if (str == null) return pderrvalue();
            return (pdstringvalue(str));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfHash
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfHash

        private delegate bool f_pdhashatomtovalue_iterator(PdfStandardAtoms atom, t_pdvalue value, object cookie);

        private class PdfHashConst
        {
            public const int kSomeReasonableInitialSize = 10;
        }

        private class t_bucket
        {
	        public t_pdvalue value;
	        public PdfStandardAtoms key;
        }

        private class t_pdhashatomtovalue
        {
	        public t_pdallocsys alloc;
	        public int limit;
	        public int elements;
	        public t_bucket[] buckets;
        }

        private void init_table(t_pdhashatomtovalue hash, int size)
        {
	        UInt32 i;
            hash.buckets = new t_bucket[size];
	        if (hash.buckets == null) return;
	        hash.limit = size;
	        hash.elements = 0;
	        for (i = 0; i < size; i++)
	        {
                hash.buckets[i] = new t_bucket();
		        hash.buckets[i].key = PdfStandardAtoms.PDA_UNDEFINED_ATOM;
                hash.buckets[i].value = new t_pdvalue();
		        hash.buckets[i].value.value.intvalue = 0;
	        }
        }

        private t_pdhashatomtovalue pd_hashatomtovalue_new(t_pdallocsys alloc, int initialsize)
        {
	        t_pdhashatomtovalue hash;
	        if (alloc == null) return (null);
            hash = new t_pdhashatomtovalue();
	        if (hash == null) return (null);
	        hash.alloc = alloc;
	        hash.elements = 0;
	        init_table(hash, initialsize == 0 ? PdfHashConst.kSomeReasonableInitialSize : initialsize);
	        return (hash);
        }

        private void pd_hashatomtovalue_free(ref t_pdhashatomtovalue table)
        {
	        table = null;
        }

        private int hash(t_pdhashatomtovalue table, PdfStandardAtoms key)
        {
	        int count;
	        int i = (int)key % table.limit;
	        for (count = 0; count < table.limit; count++)
	        {
                if (table.buckets[i].key == key)
                {
                    return (i);
                }
		        if (table.buckets[i].key == PdfStandardAtoms.PDA_UNDEFINED_ATOM)
		        {
			        return (i);
		        }
		        i = (i + 1) % table.limit;
	        }
	        return (i); // won't happen
        }

        private void rehash_table(t_bucket[] buckets, int n, t_pdhashatomtovalue table)
        {
            UInt32 ii = 0;
	        while (n-- > 0)
	        {
                if (buckets[ii].key != PdfStandardAtoms.PDA_UNDEFINED_ATOM)
                {
                    pd_hashatomtovalue_put(table, buckets[ii].key, buckets[ii].value);
                }
		        ii++;
	        }
        }

        private void pd_hashatomtovalue_put(t_pdhashatomtovalue table, PdfStandardAtoms key, t_pdvalue value)
        {
	        int index;
	        if (table == null) return;
	        if (key == PdfStandardAtoms.PDA_UNDEFINED_ATOM) return;

	        index = hash(table, key);
	        if ((table.buckets[index].key != key) && (table.elements > (table.limit * 3) / 4))
	        {
		        int oldlimit = table.limit;
		        t_bucket[] oldbuckets = table.buckets;
		        init_table(table, (oldlimit * 5) / 2); /* reasonable ? */
		        rehash_table(oldbuckets, oldlimit, table);
                oldbuckets = null;
		        /* try again */
		        pd_hashatomtovalue_put(table, key, value);
	        }
	        else
            {
		        table.buckets[index].key = key;
		        table.buckets[index].value = value;
	        }
        }

        private t_pdvalue pd_hashatomtovalue_get(t_pdhashatomtovalue table, PdfStandardAtoms key, ref bool success)
        {
	        int index;
            if (table == null) return (pderrvalue());
	        if (key == PdfStandardAtoms.PDA_UNDEFINED_ATOM)
            {
		        success = false;
		        return (pderrvalue());
	        }

	        index = hash(table, key);
	        success = table.buckets[index].key == key;
	        return ((success) ? table.buckets[index].value : pderrvalue());
        }

        private bool pd_hashatomtovalue_contains(t_pdhashatomtovalue table, PdfStandardAtoms key)
        {
	        bool success = false;
	        pd_hashatomtovalue_get(table, key, ref success);
	        return (success);
        }

        private t_pdallocsys pd_hashatomtovalue_getallocsys(t_pdhashatomtovalue table)
        {
	        return (table.alloc);
        }

        private void pd_hashatomtovalue_foreach(t_pdhashatomtovalue table, f_pdhashatomtovalue_iterator iter, object cookie)
        {
	        UInt32 i;
	        if ((iter == null) || (table == null)) return;
	        for (i = 0; i < table.limit; i++)
	        {
		        if (table.buckets[i].key != PdfStandardAtoms.PDA_UNDEFINED_ATOM)
		        {
                    if (!iter(table.buckets[i].key, table.buckets[i].value, cookie))
                    {
                        break;
                    }
		        }
	        }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfOs
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfOs
        
        public delegate byte[] fAllocate(int bytes);
        public delegate void fFree(ref byte[] ptr);
        public delegate void fReportError(/* TODO */);
        public delegate long fOutputWriter(byte[] data, long offset, long length, object cookie);
        public delegate void fMemSet(byte[] ptr, byte value, int count);
        public delegate void fMemClear(byte[] ptr, int count);

        public class t_OS
        {
            public t_OS()
            {
	            alloc = null;
                free = null;
                reportError = null;
                writeout = null;
                writeoutcookie = null;
                memset = null;
                memclear = null;
                allocsys = null;
            }

	        public fAllocate alloc;
	        public fFree free;
	        public fReportError reportError;
	        public fOutputWriter writeout;
            public object writeoutcookie;
	        public fMemSet memset;
	        public fMemClear memclear;
	        public t_pdallocsys allocsys;
        }

        private static UInt32 pdstrlen(string s)
        {
            return ((UInt32)s.Length);
        }

        /* see http://stackoverflow.com/a/7097567/20481 */
        /**
        * Double to ASCII
        */

        private string pdftoa(double n)
        {
	        return (pdftoaprecision(n, 0.00000000000001));
        }

        private static string pdftoaprecision(double n, double precision)
        {
	        /* handle special cases */
	        if (double.IsNaN(n))
            {
                return ("nan");
	        }

	        if (double.IsInfinity(n))
            {
                return ("inf");
	        }

	        if (n == 0.0)
            {
                return ("0");
	        }

            return (precision.ToString("0.00000000000000"));
        }

        private char hexdigit(int c)
        {
	        string digits = "0123456789ABCDEF";
	        return digits[c & 0xf];
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfStandardAtoms
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfStandardAtoms

        private enum PdfStandardAtoms
        {
            PDA_UNDEFINED_ATOM = 0,
            PDA_TYPE = 1,
            PDA_PAGES = 2,
            PDA_SIZE = 3,
            PDA_ROOT = 4,
            PDA_INFO = 5,
            PDA_ID = 6,
            PDA_CATALOG = 7,
            PDA_PARENT = 8,
            PDA_KIDS = 9,
            PDA_COUNT = 10,
            PDA_PAGE = 11,
            PDA_RESOURCES = 12,
            PDA_MEDIABOX = 13,
            PDA_CROPBOX = 14,
            PDA_CONTENTS = 15,
            PDA_ROTATE = 16,
            PDA_LENGTH = 17,
            PDA_FILTER = 18,
            PDA_DECODEPARMS = 19,
            PDA_SUBTYPE = 20,
            PDA_WIDTH = 21,
            PDA_HEIGHT = 22,
            PDA_BITSPERCOMPONENT = 23,
            PDA_COLORSPACE = 24,
            PDA_IMAGE = 25,
            PDA_XOBJECT = 26,
            PDA_TITLE = 27,
            PDA_SUBJECT = 28,
            PDA_AUTHOR = 29,
            PDA_KEYWORDS = 30,
            PDA_CREATOR = 31,
            PDA_PRODUCER = 32,
            PDA_NONE = 33,
            PDA_FLATEDECODE = 34,
            PDA_CCITTFAXDECODE = 35,
            PDA_DCTDECODE = 36,
            PDA_JBIG2DECODE = 37,
            PDA_JPXDECODE = 38,
            PDA_K = 39,
            PDA_COLUMNS = 40,
            PDA_ROWS = 41,
            PDA_BLACKIS1 = 42,
            PDA_DEVICEGRAY = 43,
            PDA_DEVICERGB = 44,
            PDA_DEVICECMYK = 45,
            PDA_INDEXED = 46,
            PDA_ICCBASED = 47,
            PDA_STRIP0 = 48,

            LAST_STATIC_ATOM = PDA_STRIP0
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfStandardObjects
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfStandardObjects

        private enum e_ImageCompression
        {
	        kCompNone,
	        kCompFlate,
	        kCompCCITT,
	        kCompDCT,
	        kCompJBIG2,
	        kCompJPX
        }

        private enum e_ColorSpace
        {
	        kDeviceGray,
	        kDeviceRgb,
	        kDeviceCmyk,
	        kIndexed,
	        kIccBased,
        }

        private enum e_CCITTKind
        {
	        kCCIITTG4,
	        kCCITTG31D,
	        kCCITTG32D
        }

        private t_pdvalue pd_catalog_new(t_pdallocsys alloc, t_pdxref xref)
        {
            t_pdvalue catdict = dict_new(alloc, 20);
            t_pdvalue pagesdict = dict_new(alloc, 3);
            dict_put(catdict, PdfStandardAtoms.PDA_TYPE, pdatomvalue(PdfStandardAtoms.PDA_CATALOG));
            dict_put(catdict, PdfStandardAtoms.PDA_PAGES, pd_xref_makereference(xref, pagesdict));

            dict_put(pagesdict, PdfStandardAtoms.PDA_TYPE, pdatomvalue(PdfStandardAtoms.PDA_PAGES));
            dict_put(pagesdict, PdfStandardAtoms.PDA_KIDS, pdarrayvalue(pd_array_new(alloc, 10)));
            dict_put(pagesdict, PdfStandardAtoms.PDA_COUNT, pdintvalue(0));

            return (pd_xref_makereference(xref, catdict));
        }

        private t_pdvalue pd_info_new(t_pdallocsys alloc, t_pdxref xref, string title, string author, string subject, string keywords, string creator, string producer)
        {
            t_pdvalue infodict = dict_new(alloc, 8);
            dict_put(infodict, PdfStandardAtoms.PDA_TYPE, pdatomvalue(PdfStandardAtoms.PDA_INFO));
            if (title != null)      dict_put(infodict, PdfStandardAtoms.PDA_TITLE, pdcstrvalue(alloc, title));
            if (author != null)     dict_put(infodict, PdfStandardAtoms.PDA_AUTHOR, pdcstrvalue(alloc, author));
            if (subject != null)    dict_put(infodict, PdfStandardAtoms.PDA_SUBJECT, pdcstrvalue(alloc, subject));
            if (keywords != null)   dict_put(infodict, PdfStandardAtoms.PDA_KEYWORDS, pdcstrvalue(alloc, keywords));
            if (creator != null)    dict_put(infodict, PdfStandardAtoms.PDA_CREATOR, pdcstrvalue(alloc, creator));
            if (producer != null)   dict_put(infodict, PdfStandardAtoms.PDA_PRODUCER, pdcstrvalue(alloc, producer));
            return pd_xref_makereference(xref, infodict);
        }

        private t_pdvalue pd_trailer_new(t_pdallocsys alloc, t_pdxref xref, t_pdvalue catalog, t_pdvalue info)
        {
            t_pdvalue trailer = dict_new(alloc, 4);
            dict_put(trailer, PdfStandardAtoms.PDA_SIZE, pdintvalue((int)pd_xref_size(xref)));
            dict_put(trailer, PdfStandardAtoms.PDA_ROOT, catalog);
            if (!IS_ERR(info))
            {
                dict_put(trailer, PdfStandardAtoms.PDA_INFO, info);
            }
            return (trailer);
        }

        private static PdfStandardAtoms ToCompressionAtom(e_ImageCompression comp)
        {
            switch (comp)
            {
                default:
                case e_ImageCompression.kCompNone: return (PdfStandardAtoms.PDA_NONE);
                case e_ImageCompression.kCompFlate: return (PdfStandardAtoms.PDA_FLATEDECODE);
                case e_ImageCompression.kCompCCITT: return (PdfStandardAtoms.PDA_CCITTFAXDECODE);
                case e_ImageCompression.kCompDCT: return (PdfStandardAtoms.PDA_DCTDECODE);
                case e_ImageCompression.kCompJBIG2: return (PdfStandardAtoms.PDA_JBIG2DECODE);
                case e_ImageCompression.kCompJPX: return (PdfStandardAtoms.PDA_JPXDECODE);
            }
        }

        private t_pdvalue pd_image_new
        (
            t_pdallocsys alloc,
            t_pdxref xref,
            f_on_datasink_ready ready,
            object eventcookie,
            t_pdvalue width,
            t_pdvalue height,
            t_pdvalue bitspercomponent,
            e_ImageCompression comp,
            t_pdvalue compParms,
            t_pdvalue colorspace
        )
        {
	        t_pdvalue image = stream_new(alloc, xref, 10, ready, eventcookie);
	        t_pdarray filter;
	        t_pdarray filterparms;
	        if (IS_ERR(image)) return (image);
            dict_put(image, PdfStandardAtoms.PDA_TYPE, pdatomvalue(PdfStandardAtoms.PDA_XOBJECT));
            dict_put(image, PdfStandardAtoms.PDA_SUBTYPE, pdatomvalue(PdfStandardAtoms.PDA_IMAGE));
            dict_put(image, PdfStandardAtoms.PDA_WIDTH, width);
            dict_put(image, PdfStandardAtoms.PDA_HEIGHT, height);
            dict_put(image, PdfStandardAtoms.PDA_BITSPERCOMPONENT, bitspercomponent);
	        filter = pd_array_new(alloc, 1);
            if (comp != e_ImageCompression.kCompNone)
	        {
		        pd_array_add(filter, pdatomvalue(ToCompressionAtom(comp)));
                dict_put(image, PdfStandardAtoms.PDA_FILTER, pdarrayvalue(filter));
		        filterparms = pd_array_new(alloc, 1);
                if (!IS_NULL(compParms))
                {
                    pd_array_add(filterparms, compParms);
                }
                dict_put(image, PdfStandardAtoms.PDA_DECODEPARMS, pdarrayvalue(filterparms));
	        }
            dict_put(image, PdfStandardAtoms.PDA_COLORSPACE, colorspace);
            dict_put(image, PdfStandardAtoms.PDA_LENGTH, pd_xref_makereference(xref, pdintvalue(0)));

	        return image;
        }

        private static int ToK(e_CCITTKind kind)
        {
            switch (kind)
            {
                default:
                case e_CCITTKind.kCCIITTG4: return (-1);
                case e_CCITTKind.kCCITTG31D: return (0);
                case e_CCITTKind.kCCITTG32D: return (1);
            }
        }

        private t_pdvalue MakeCCITTParms(t_pdallocsys alloc, UInt32 width, UInt32 height, e_CCITTKind kind, bool ccittBlackIs1)
        {
            t_pdvalue parms = dict_new(alloc, 4);
            dict_put(parms, PdfStandardAtoms.PDA_K, pdintvalue(ToK(kind)));
            dict_put(parms, PdfStandardAtoms.PDA_COLUMNS, pdintvalue((int)width));
            dict_put(parms, PdfStandardAtoms.PDA_ROWS, pdintvalue((int)height));
            dict_put(parms, PdfStandardAtoms.PDA_BLACKIS1, pdboolvalue(ccittBlackIs1));
            return (parms);
        }

        private static PdfStandardAtoms ToColorSpaceAtom(e_ColorSpace cs)
        {
            switch (cs)
            {
                default: /* TODO FAIL */
                case e_ColorSpace.kDeviceGray: return (PdfStandardAtoms.PDA_DEVICEGRAY);
                case e_ColorSpace.kDeviceRgb: return (PdfStandardAtoms.PDA_DEVICERGB);
                case e_ColorSpace.kDeviceCmyk: return (PdfStandardAtoms.PDA_DEVICECMYK);
            }
        }

        private t_pdvalue pd_image_new_simple
        (
            t_pdallocsys alloc,
            t_pdxref xref,
            f_on_datasink_ready ready,
            object eventcookie,
            UInt32 width,
            UInt32 height,
            UInt32 bitspercomponent,
            e_ImageCompression comp,
            e_CCITTKind kind,
            bool ccittBlackIs1,
            e_ColorSpace colorspace
        )
        {
            t_pdvalue cs = pdatomvalue(ToColorSpaceAtom(colorspace));
            t_pdvalue comparms = (comp == e_ImageCompression.kCompCCITT) ? MakeCCITTParms(alloc, width, height, kind, ccittBlackIs1) : pdnullvalue();
            return
            (
                pd_image_new
                (
                    alloc,
                    xref,
                    ready,
                    eventcookie,
                    pdintvalue((int)width),
                    pdintvalue((int)height),
                    pdintvalue((int)bitspercomponent),
                    comp,
                    comparms,
                    cs
                )
            );
        }

        private t_pdvalue pd_page_new_simple(t_pdallocsys alloc, t_pdxref xref, t_pdvalue catalog, double width, double height)
        {
            bool succ = false;
            t_pdvalue pagedict = dict_new(alloc, 20);
            t_pdvalue pagesdict = dict_get(catalog, PdfStandardAtoms.PDA_PAGES, ref succ); /* this is a reference */
            t_pdvalue resources = dict_new(alloc, 1);
            //assert(IS_REFERENCE(pagesdict));

            dict_put(pagedict, PdfStandardAtoms.PDA_TYPE, pdatomvalue(PdfStandardAtoms.PDA_PAGE));
            dict_put(pagedict, PdfStandardAtoms.PDA_PARENT, pagesdict);
            dict_put(pagedict, PdfStandardAtoms.PDA_MEDIABOX, pdarrayvalue(pd_array_buildfloats(alloc, 4, new double[] { 0.0, 0.0, width, height })));
            dict_put(pagedict, PdfStandardAtoms.PDA_RESOURCES, resources);
            dict_put(resources, PdfStandardAtoms.PDA_XOBJECT, dict_new(alloc, 20));
            return (pd_xref_makereference(xref, pagedict));
        }

        private void pd_catalog_add_page(t_pdvalue catalog, t_pdvalue page)
        {
            bool succ = false;
            t_pdvalue pagesdict = dict_get(catalog, PdfStandardAtoms.PDA_PAGES, ref succ); /* this is a reference */
            t_pdvalue kidsarr = dict_get(pagesdict, PdfStandardAtoms.PDA_KIDS, ref succ);
            t_pdvalue count = dict_get(pagesdict, PdfStandardAtoms.PDA_COUNT, ref succ);
            //assert(IS_REFERENCE(pagesdict));
            //assert(IS_DICT(pd_reference_get_value(pagesdict.value.refvalue)));
            //assert(IS_ARRAY(kidsarr));
            //assert(IS_INT(count));
            // append the new page to the /Kids array
            pd_array_add(kidsarr.value.arrvalue, page);
            // increment the total page count
            dict_put(pagesdict, PdfStandardAtoms.PDA_COUNT, pdintvalue(count.value.intvalue)); // tbd: was "count.value.intvalue + 1"
        }

        private t_pdvalue pd_contents_new(t_pdallocsys alloc, t_pdxref xref, t_pdcontents_gen gen)
        {
            t_pdvalue contents = stream_new(alloc, xref, 0, pd_contents_generate, gen);
            dict_put(contents, PdfStandardAtoms.PDA_LENGTH, pd_xref_makereference(xref, pdintvalue(0)));
            return (contents);
        }

        private void pd_page_add_image(t_pdvalue page, PdfStandardAtoms imageatom, t_pdvalue image)
        {
            bool succ = false;
            dict_put(dict_get(dict_get(page, PdfStandardAtoms.PDA_RESOURCES, ref succ), PdfStandardAtoms.PDA_XOBJECT, ref succ), imageatom, image);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfStreaming
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfStreaming

        private class t_pdoutstream
        {
	        public t_pdallocsys alloc;
            public fOutputWriter writer;
            public object writercookie;
            public UInt32 pos;
        }

        private t_pdoutstream pd_outstream_new(t_pdallocsys allocsys, t_OS os)
        {
	        t_pdoutstream stm = new t_pdoutstream();
	        if (stm != null)
	        {
		        stm.alloc = allocsys;
		        stm.writer = os.writeout;
		        stm.writercookie = os.writeoutcookie;
		        stm.pos = 0;
	        }
	        return (stm);
        }

        private void pd_outstream_free(ref t_pdoutstream stm)
        {
	        if (stm == null) return;
	        stm = null;
        }

        private static void pd_putc(t_pdoutstream stm, byte c)
        {
            byte[] __buf = new byte[1];
	        if (stm == null) return;
	        __buf[0] = c;
	        stm.writer(__buf, 0, 1, stm.writercookie);
	        stm.pos += 1;
        }

        private void pd_puts(t_pdoutstream stm, string s)
        {
	        if ((stm == null) || (s == null) || (s.Length == 0)) return;
            byte[] ab = Encoding.UTF8.GetBytes(s);
	        pd_putn(stm, ab, 0, (UInt32)ab.Length);
        }

        private static void pd_putn(t_pdoutstream stm, byte[] s, UInt32 offset, UInt32 len)
        {
	        if (stm == null) return;
	        stm.writer(s, offset, len, stm.writercookie);
	        stm.pos += len;
        }

        private void pd_putint(t_pdoutstream stm, Int32 i)
        {
	        pd_puts(stm, i.ToString());
        }

        private void pd_putfloat(t_pdoutstream stm, double d)
        {
	        pd_puts(stm, d.ToString());
        }

        private static UInt32 pd_outstream_pos(t_pdoutstream stm)
        {
	        if (stm == null) return (0);
	        return (stm.pos);
        }

        private void writeatom(t_pdoutstream os, UInt32 atom)
        {
	        string str;
            int ii = 0;
	        pd_putc(os, (byte)'/');
	        str = pd_string_from_atom(atom);
            byte[] ab = Encoding.UTF8.GetBytes(str);
	        while (ii < ab.Length)
	        {
                if ((ab[ii] < 0x21) || (ab[ii] > '~') || (ab[ii] == '#') || (ab[ii] == '%') || (ab[ii] == '/'))
		        {
                    pd_putc(os, (byte)'#');
                    pd_putc(os, (byte)hexdigit(str[ii] >> 4));
                    pd_putc(os, (byte)hexdigit(str[ii]));
		        }
		        else
		        {
                    pd_putc(os, (byte)str[ii]);
		        }
		        ii += 1;
	        }
        }

        private bool itemwriter(PdfStandardAtoms key, t_pdvalue value, object cookie)
        {
            t_pdoutstream os = (t_pdoutstream)cookie;
	        if (os == null) return (false);
	        pd_putc(os, (byte)' ');
	        writeatom(os, (UInt32)key);
	        pd_putc(os, (byte)' ');
	        pd_write_value(os, value);
	        return (true);
        }

        private void writedict(t_pdoutstream os, t_pdvalue dict)
        {
	        if (!IS_DICT(dict)) return;
	        pd_puts(os, "<<");
	        dict_foreach(dict, itemwriter, os);
	        pd_puts(os, " >>");
	        if (dict_is_stream(dict))
	        {
		        stream_write_contents(dict, os);
	        }
        }

        private bool arritemwriter(t_pdarray arr, UInt32 currindex, t_pdvalue value, object cookie)
        {
            t_pdoutstream os = (t_pdoutstream)cookie;
	        if (os == null) return (false);
	        pd_putc(os, (byte)' ');
	        pd_write_value(os, value);
	        return (true);
        }

        private void writearray(t_pdoutstream os, t_pdvalue arr)
        {
	        if (!IS_ARRAY(arr)) return;
	        pd_puts(os, "[");
	        pd_array_foreach(arr.value.arrvalue, arritemwriter, os);
	        pd_puts(os, " ]");
        }

        private void writeesc(t_pdoutstream stm, byte c)
        {
	        pd_putc(stm, (byte)'\\');
	        if (c < ' ')
	        {
                pd_putc(stm, (byte)'0');
                pd_putc(stm, (byte)((byte)'0' + (byte)((c >> 3) & 7)));
                pd_putc(stm, (byte)((byte)'0' + (byte)(c & 7)));
	        }
	        else
            {
		        pd_putc(stm, c);
	        }
        }

        private bool asciter(UInt32 index, byte c, t_pdoutstream stm)
        {
            if ((c < ' ') || (c == '(') || (c == ')') || (c == '\\'))
            {
                writeesc(stm, c);
            }
            else
            {
                pd_putc(stm, c);
            }
            return (true);
        }

        private bool hexiter(UInt32 index, byte c, t_pdoutstream stm)
        {
	        pd_putc(stm, (byte)hexdigit(c >> 4));
            pd_putc(stm, (byte)hexdigit(c));
	        return (true);
        }

        private void writestring(t_pdoutstream stm, t_pdstring str)
        {
	        if (pd_string_is_binary(str))
	        {
                pd_putc(stm, (byte)'<');
		        pd_string_foreach(str, hexiter, stm);
                pd_putc(stm, (byte)'>');
	        }
	        else
	        {
                pd_putc(stm, (byte)'(');
		        pd_string_foreach(str, asciter, stm);
                pd_putc(stm, (byte)')');
	        }
        }

        private void pd_write_value(t_pdoutstream stm, t_pdvalue value)
        {
	        if (stm == null) return;
	        switch (value.pdtype)
	        {
                case t_pdtype.TPDARRAY: writearray(stm, value); break;
                case t_pdtype.TPDNULL: pd_puts(stm, "null"); break;
                case t_pdtype.TPDBOOL: pd_puts(stm, (value.value.boolvalue ? "true" : "false")); break;
                case t_pdtype.TPDNUMBERINT: pd_putint(stm, value.value.intvalue); break;
                case t_pdtype.TPDNUMBERFLOAT: pd_putfloat(stm, value.value.floatvalue); break;
                case t_pdtype.TPDDICT: writedict(stm, value); break;
                case t_pdtype.TPDNAME: writeatom(stm, value.value.namevalue); break;
                case t_pdtype.TPDREFERENCE:
		            pd_putint(stm, (Int32)pd_reference_object_number(value.value.refvalue));
		            pd_puts(stm, " 0 R");
		            break;
                case t_pdtype.TPDSTRING:
		            writestring(stm, value.value.stringvalue);
		            break;
	        }
        }

        private void pd_write_pdreference_declaration(t_pdoutstream stm, t_pdreference r)
        {
	        if ((stm == null) || (r == null) || pd_reference_is_written(r)) return;
	        pd_reference_set_position(r, stm.pos);
	        pd_putint(stm, (Int32)pd_reference_object_number(r));
	        pd_puts(stm, " 0 obj ");
	        pd_write_value(stm, pd_reference_get_value(r));
	        pd_puts(stm, "\nendobj\n");
	        pd_reference_mark_written(r);
        }

        private void pd_write_reference_declaration(t_pdoutstream stm, t_pdvalue value)
        {
	        if (!IS_REFERENCE(value)) return;
	        pd_write_pdreference_declaration(stm, value.value.refvalue);
        }

        private void pd_write_pdf_header(t_pdoutstream stm, string version, string line2)
        {
	        if (line2 == null)
            {
		        line2 = "%\xE2\xE3\xCF\xD3\n";
	        }
	        pd_puts(stm, "%PDF-");
	        pd_puts(stm, version);
	        pd_putc(stm, (byte)'\n');
	        pd_puts(stm, line2);
        }

        private void pd_write_endofdocument(t_pdallocsys alloc, t_pdoutstream stm, t_pdxref xref, t_pdvalue catalog, t_pdvalue info)
        {
	        t_pdvalue trailer = pd_trailer_new(alloc, xref, catalog, info);
	        pd_xref_writeallpendingreferences(xref, stm);
	        UInt32 pos = pd_outstream_pos(stm);
	        pd_xref_writetable(xref, stm);
	        pd_puts(stm, "trailer\n");
	        pd_write_value(stm, trailer);
	        pd_putc(stm, (byte)'\n');
	        pd_puts(stm, "startxref\n");
	        pd_putint(stm, (Int32)pos);
	        pd_puts(stm, "\n%%EOF\n");
	        dict_free(trailer);
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfString
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfString

        private delegate bool f_pdstring_foreach(UInt32 index, byte c, t_pdoutstream stm);

        private class t_pdstring
        {
            public t_pdstring()
            {
	            alloc = null;
	            isBinary = false;
	            length = 0;
	            strData = null;
            }

	        public t_pdallocsys alloc;
	        public bool isBinary;
	        public int length;
	        public byte[] strData;
        }

        private t_pdstring pd_string_new(t_pdallocsys alloc, byte[] sz, UInt32 len, bool isbinary)
        {
	        t_pdstring str;
	        if ((alloc == null) || (sz == null)) return (null);
            str = new t_pdstring();
	        if (str == null) return (null);
	        str.alloc = alloc;
	        str.strData = new byte[len];
	        if (str.strData == null)
	        {
		        str = null;
		        return (null);
	        }
	        str.length = (int)len;
	        pd_string_set(str, sz, len, isbinary);
	        return (str);
        }

        private static void pd_string_free(ref t_pdstring str)
        {
	        if (str == null) return;
            str.strData = null;
            str = null;
        }

        private static int pd_string_length(t_pdstring str)
        {
	        if (str == null) return (0);
	        return (str.length);
        }

        private static void pd_string_set(t_pdstring str, byte[] sz, UInt32 len, bool isbinary)
        {
	        UInt32 i;

	        if ((str == null) || (sz == null)) return;
	        if (len != str.length)
	        {
                str.strData = null;
                str.strData = new byte[len];
	        }

	        for (i = 0; i < len; i++)
	        {
		        str.strData[i] = sz[i];
	        }
        }

        private static bool pd_string_is_binary(t_pdstring str)
        {
	        if (str == null) return (false);
	        return (str.isBinary);
        }

        private static byte pdstring_char_at(t_pdstring str, UInt32 index)
        {
	        if ((str == null) || (index >= str.length)) return (0);
	        return (str.strData[index]);
        }

        private static void pd_string_foreach(t_pdstring str, f_pdstring_foreach iter, t_pdoutstream stm)
        {
	        UInt32 i;
	        if ((str == null) || (iter == null)) return;
	        for (i = 0; i < str.length; i++)
	        {
		        if (!iter(i, str.strData[i], stm))
                {
			        break;
                }
	        }
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfStrings
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfStrings

        private static string pd_strdup(t_pdallocsys alloc, string str)
        {
            if (str == null) return (null);
            return (string.Copy(str));
        }

        private static int pd_strcmp(string s1, string s2)
        {
            return (s1.CompareTo(s2));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private: PdfXrefTable
        ///////////////////////////////////////////////////////////////////////////////
        #region Private: PdfXrefTable

        private class t_pdreference
        {
	        public UInt16 isWritten;
	        public UInt16 objectNumber;
	        public UInt32 pos;
	        public t_pdvalue value;
        }

        private static UInt32 pd_reference_object_number(t_pdreference r)
        {
	        if (r == null) return (0);
	        return (r.objectNumber);
        }

        private static bool pd_reference_is_written(t_pdreference r)
        {
	        if (r == null) return (false);
	        return ((r.isWritten != 0) ? true : false);
        }

        private static void pd_reference_mark_written(t_pdreference r)
        {
	        if (r == null) return; /* TODO FAIL */
	        r.isWritten = 1;
        }

        private t_pdvalue pd_reference_get_value(t_pdreference r)
        {
	        if (r == null) return pderrvalue();
	        return (r.value);
        }

        private static void pd_reference_set_value(t_pdreference r, t_pdvalue value)
        {
	        if (r == null) return; /* TODO FAIL */
	        r.value = value;
        }

        private static UInt32 pd_reference_get_position(t_pdreference r)
        {
	        if (r == null) return (0); /* TODO FAIL */
	        return (r.pos);
        }

        private static void pd_reference_set_position(t_pdreference r, UInt32 pos)
        {
	        if (r == null) return; /* TODO FAIL */
	        r.pos = pos;
        }

        private class t_xr
        {
	        public t_pdreference reference;
	        public t_xr next;
        }

        private class t_pdxref
        {
	        public t_pdallocsys alloc;
	        public UInt32 nextObjectNumber;
	        public t_xr first;
	        public t_xr last;
        }

        private static t_pdxref pd_xref_new(t_pdallocsys alloc)
        {
            t_pdxref xref = new t_pdxref();
	        if (xref == null) return (null);
	        xref.alloc = alloc;
	        xref.nextObjectNumber = 1;
	        return (xref);
        }

        private static void pd_xref_free(ref t_pdxref xref)
        {
	        t_xr walker;
	        t_xr next;
	        if (xref == null) return;
	        walker = xref.first;
	        while (walker != null)
	        {
		        next = walker.next;
		        if (walker.reference != null)
                {
                    walker.reference = null;
                }
                walker = null;
		        walker = next;
	        }
            xref = null;
        }

        private static bool match(t_pdreference r, t_pdvalue value)
        {
	        if (r.value.pdtype != value.pdtype) return (false);
	        switch (value.pdtype)
	        {
                case t_pdtype.TPDDICT: return (value.value.dictvalue == r.value.value.dictvalue);
                case t_pdtype.TPDARRAY: return (value.value.arrvalue == r.value.value.arrvalue);
	            default: break;
	        }
	        return (false);
        }

        private t_xr findmatch(t_pdxref xref, t_pdvalue value)
        {
	        t_xr walker;
	        for (walker = xref.first; walker != null; walker = walker.next)
	        {
		        if (match(walker.reference, value)) return (walker);
	        }
	        return (null);
        }

        private t_pdvalue pd_xref_makereference(t_pdxref xref, t_pdvalue value)
        {
	        t_xr xr;
	        if (xref == null) return (pdnullvalue());
	        if (IS_REFERENCE(value)) return (value);
	        xr = findmatch(xref, value);
	        if (xr == null)
            {
                xr = new t_xr();
                xr.reference = new t_pdreference();
		        xr.reference.value = value;
		        xr.reference.objectNumber = (ushort)(xref.nextObjectNumber++);
		        if (xref.first == null)
		        {
			        xref.first = xref.last = xr;
		        }
		        else
                {
			        xref.last.next = xr;
			        xref.last = xr;
		        }
	        }
            t_pdvalue val = new t_pdvalue();
            val.pdtype = t_pdtype.TPDREFERENCE;
            val.value.refvalue = xr.reference;
	        return (val);
        }

        private bool pd_xref_isreferenced(t_pdxref xref, t_pdvalue value)
        {
	        if (IS_REFERENCE(value)) return (false);
	        return (findmatch(xref, value) != null);
        }

        private static UInt32 xref_size(t_pdxref xref)
        {
	        t_xr walker;
	        UInt32 i = 0;
	        if (xref == null) return (0);
	        for (walker = xref.first; walker != null; walker = walker.next)
	        {
		        i++;
	        }
	        return (i);
        }

        private void write_entry(t_pdoutstream os, UInt32 pos, string gen, char status)
        {
	        string s = pos.ToString();
	        int i;

            byte[] ab = Encoding.UTF8.GetBytes(s);
            for (i = 0; i < 10 - ab.Length; i++)
            {
                pd_putc(os, (byte)'0');
            }
	        pd_putn(os, ab, 0, (UInt32)ab.Length);
            pd_putc(os, (byte)' ');
	        pd_puts(os, gen);
            pd_putc(os, (byte)' ');
	        pd_putc(os, (byte)status);
            pd_putc(os, (byte)'\r');
            pd_putc(os, (byte)'\n');
        }

        private void pd_xref_writeallpendingreferences(t_pdxref xref, t_pdoutstream os)
        {
	        t_xr walker;
	        if ((xref == null) || (os == null)) return;
	        for (walker = xref.first; walker != null; walker = walker.next)
	        {
		        pd_write_pdreference_declaration(os, walker.reference);
	        }
        }

        private void pd_xref_writetable(t_pdxref xref, t_pdoutstream os)
        {
	        UInt32 size = xref_size(xref);
	        t_xr walker;
	        pd_puts(os, "xref\n");
	        pd_putint(os, 0);
            pd_putc(os, (byte)' ');
	        pd_putint(os, (Int32)(size + 1));
	        pd_putc(os, (byte)'\n');
	        write_entry(os, 0, "65535", 'f');
	        for (walker = xref.first; walker != null; walker = walker.next)
	        {
		        write_entry(os, walker.reference.pos, "00000", 'n');
	        }
        }

        private UInt32 pd_xref_size(t_pdxref xref)
        {
	        if (xref == null) return (0);
	        return (xref_size(xref));
        }

        #endregion


        ///////////////////////////////////////////////////////////////////////////////
        // Private Definitions...
        ///////////////////////////////////////////////////////////////////////////////
        #region Private Definitions...

        // A TIFF header is composed of tags...
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct TiffTag
        {
            public TiffTag(ushort a_u16Tag, ushort a_u16Type, uint a_u32Count, uint a_u32Value)
            {
                u16Tag = a_u16Tag;
                u16Type = a_u16Type;
                u32Count = a_u32Count;
                u32Value = a_u32Value;
            }

            public ushort u16Tag;
            public ushort u16Type;
            public uint u32Count;
            public uint u32Value;
        }

        // TIFF header for Uncompressed BITONAL images...
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct TiffBitonalUncompressed
        {
            // Constructor...
            public TiffBitonalUncompressed(uint a_u32Width, uint a_u32Height, uint a_u32Resolution, uint a_u32Size)
            {
                // Header...
                u16ByteOrder = 0x4949;
                u16Version = 42;
                u32OffsetFirstIFD = 8;

                // First IFD...
                u16IFD = 16;

                // Tags...
                tifftagNewSubFileType = new TiffTag(254, 4, 1, 0);
                tifftagSubFileType = new TiffTag(255, 3, 1, 1);
                tifftagImageWidth = new TiffTag(256, 4, 1, a_u32Width);
                tifftagImageLength = new TiffTag(257, 4, 1, a_u32Height);
                tifftagBitsPerSample = new TiffTag(258, 3, 1, 1);
                tifftagCompression = new TiffTag(259, 3, 1, 1);
                tifftagPhotometricInterpretation = new TiffTag(262, 3, 1, 1);
                tifftagFillOrder = new TiffTag(266, 3, 1, 1);
                tifftagStripOffsets = new TiffTag(273, 4, 1, 222);
                tifftagSamplesPerPixel = new TiffTag(277, 3, 1, 1);
                tifftagRowsPerStrip = new TiffTag(278, 4, 1, a_u32Height);
                tifftagStripByteCounts = new TiffTag(279, 4, 1, a_u32Size);
                tifftagXResolution = new TiffTag(282, 5, 1, 206);
                tifftagYResolution = new TiffTag(283, 5, 1, 214);
                tifftagT4T6Options = new TiffTag(292, 4, 1, 0);
                tifftagResolutionUnit = new TiffTag(296, 3, 1, 2);

                // Footer...
                u32NextIFD = 0;
                u64XResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
                u64YResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
            }

            // Header...
            public ushort u16ByteOrder;
            public ushort u16Version;
            public uint u32OffsetFirstIFD;

            // First IFD...
            public ushort u16IFD;

            // Tags...
            public TiffTag tifftagNewSubFileType;
            public TiffTag tifftagSubFileType;
            public TiffTag tifftagImageWidth;
            public TiffTag tifftagImageLength;
            public TiffTag tifftagBitsPerSample;
            public TiffTag tifftagCompression;
            public TiffTag tifftagPhotometricInterpretation;
            public TiffTag tifftagFillOrder;
            public TiffTag tifftagStripOffsets;
            public TiffTag tifftagSamplesPerPixel;
            public TiffTag tifftagRowsPerStrip;
            public TiffTag tifftagStripByteCounts;
            public TiffTag tifftagXResolution;
            public TiffTag tifftagYResolution;
            public TiffTag tifftagT4T6Options;
            public TiffTag tifftagResolutionUnit;

            // Footer...
            public uint u32NextIFD;
            public ulong u64XResolution;
            public ulong u64YResolution;
        }

        // TIFF header for Group4 BITONAL images...
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct TiffBitonalG4
        {
            // Constructor...
            public TiffBitonalG4(uint a_u32Width, uint a_u32Height, uint a_u32Resolution, uint a_u32Size)
            {
                // Header...
                u16ByteOrder = 0x4949;
                u16Version = 42;
                u32OffsetFirstIFD = 8;

                // First IFD...
                u16IFD = 16;

                // Tags...
                tifftagNewSubFileType = new TiffTag(254, 4, 1, 0);
                tifftagSubFileType = new TiffTag(255, 3, 1, 1);
                tifftagImageWidth = new TiffTag(256, 4, 1, a_u32Width);
                tifftagImageLength = new TiffTag(257, 4, 1, a_u32Height);
                tifftagBitsPerSample = new TiffTag(258, 3, 1, 1);
                tifftagCompression = new TiffTag(259, 3, 1, 4);
                tifftagPhotometricInterpretation = new TiffTag(262, 3, 1, 0);
                tifftagFillOrder = new TiffTag(266, 3, 1, 1);
                tifftagStripOffsets = new TiffTag(273, 4, 1, 222);
                tifftagSamplesPerPixel = new TiffTag(277, 3, 1, 1);
                tifftagRowsPerStrip = new TiffTag(278, 4, 1, a_u32Height);
                tifftagStripByteCounts = new TiffTag(279, 4, 1, a_u32Size);
                tifftagXResolution = new TiffTag(282, 5, 1, 206);
                tifftagYResolution = new TiffTag(283, 5, 1, 214);
                tifftagT4T6Options = new TiffTag(293, 4, 1, 0);
                tifftagResolutionUnit = new TiffTag(296, 3, 1, 2);

                // Footer...
                u32NextIFD = 0;
                u64XResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
                u64YResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
            }

            // Header...
            public ushort u16ByteOrder;
            public ushort u16Version;
            public uint u32OffsetFirstIFD;

            // First IFD...
            public ushort u16IFD;

            // Tags...
            public TiffTag tifftagNewSubFileType;
            public TiffTag tifftagSubFileType;
            public TiffTag tifftagImageWidth;
            public TiffTag tifftagImageLength;
            public TiffTag tifftagBitsPerSample;
            public TiffTag tifftagCompression;
            public TiffTag tifftagPhotometricInterpretation;
            public TiffTag tifftagFillOrder;
            public TiffTag tifftagStripOffsets;
            public TiffTag tifftagSamplesPerPixel;
            public TiffTag tifftagRowsPerStrip;
            public TiffTag tifftagStripByteCounts;
            public TiffTag tifftagXResolution;
            public TiffTag tifftagYResolution;
            public TiffTag tifftagT4T6Options;
            public TiffTag tifftagResolutionUnit;

            // Footer...
            public uint u32NextIFD;
            public ulong u64XResolution;
            public ulong u64YResolution;
        }

        // TIFF header for Uncompressed GRAYSCALE images...
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct TiffGrayscaleUncompressed
        {
            // Constructor...
            public TiffGrayscaleUncompressed(uint a_u32Width, uint a_u32Height, uint a_u32Resolution, uint a_u32Size)
            {
                // Header...
                u16ByteOrder = 0x4949;
                u16Version = 42;
                u32OffsetFirstIFD = 8;

                // First IFD...
                u16IFD = 14;

                // Tags...
                tifftagNewSubFileType = new TiffTag(254, 4, 1, 0);
                tifftagSubFileType = new TiffTag(255, 3, 1, 1);
                tifftagImageWidth = new TiffTag(256, 4, 1, a_u32Width);
                tifftagImageLength = new TiffTag(257, 4, 1, a_u32Height);
                tifftagBitsPerSample = new TiffTag(258, 3, 1, 8);
                tifftagCompression = new TiffTag(259, 3, 1, 1);
                tifftagPhotometricInterpretation = new TiffTag(262, 3, 1, 1);
                tifftagStripOffsets = new TiffTag(273, 4, 1, 198);
                tifftagSamplesPerPixel = new TiffTag(277, 3, 1, 1);
                tifftagRowsPerStrip = new TiffTag(278, 4, 1, a_u32Height);
                tifftagStripByteCounts = new TiffTag(279, 4, 1, a_u32Size);
                tifftagXResolution = new TiffTag(282, 5, 1, 182);
                tifftagYResolution = new TiffTag(283, 5, 1, 190);
                tifftagResolutionUnit = new TiffTag(296, 3, 1, 2);

                // Footer...
                u32NextIFD = 0;
                u64XResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
                u64YResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
            }

            // Header...
            public ushort u16ByteOrder;
            public ushort u16Version;
            public uint u32OffsetFirstIFD;

            // First IFD...
            public ushort u16IFD;

            // Tags...
            public TiffTag tifftagNewSubFileType;
            public TiffTag tifftagSubFileType;
            public TiffTag tifftagImageWidth;
            public TiffTag tifftagImageLength;
            public TiffTag tifftagBitsPerSample;
            public TiffTag tifftagCompression;
            public TiffTag tifftagPhotometricInterpretation;
            public TiffTag tifftagStripOffsets;
            public TiffTag tifftagSamplesPerPixel;
            public TiffTag tifftagRowsPerStrip;
            public TiffTag tifftagStripByteCounts;
            public TiffTag tifftagXResolution;
            public TiffTag tifftagYResolution;
            public TiffTag tifftagResolutionUnit;

            // Footer...
            public uint u32NextIFD;
            public ulong u64XResolution;
            public ulong u64YResolution;
        }

        // TIFF header for Uncompressed COLOR images...
        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct TiffColorUncompressed
        {
            // Constructor...
            public TiffColorUncompressed(uint a_u32Width, uint a_u32Height, uint a_u32Resolution, uint a_u32Size)
            {
                // Header...
                u16ByteOrder = 0x4949;
                u16Version = 42;
                u32OffsetFirstIFD = 8;

                // First IFD...
                u16IFD = 14;

                // Tags...
                tifftagNewSubFileType = new TiffTag(254, 4, 1, 0);
                tifftagSubFileType = new TiffTag(255, 3, 1, 1);
                tifftagImageWidth = new TiffTag(256, 4, 1, a_u32Width);
                tifftagImageLength = new TiffTag(257, 4, 1, a_u32Height);
                tifftagBitsPerSample = new TiffTag(258, 3, 3, 182);
                tifftagCompression = new TiffTag(259, 3, 1, 1);
                tifftagPhotometricInterpretation = new TiffTag(262, 3, 1, 2);
                tifftagStripOffsets = new TiffTag(273, 4, 1, 204);
                tifftagSamplesPerPixel = new TiffTag(277, 3, 1, 3);
                tifftagRowsPerStrip = new TiffTag(278, 4, 1, a_u32Height);
                tifftagStripByteCounts = new TiffTag(279, 4, 1, a_u32Size);
                tifftagXResolution = new TiffTag(282, 5, 1, 188);
                tifftagYResolution = new TiffTag(283, 5, 1, 196);
                tifftagResolutionUnit = new TiffTag(296, 3, 1, 2);

                // Footer...
                u32NextIFD = 0;
                u16XBitsPerSample1 = 8;
                u16XBitsPerSample2 = 8;
                u16XBitsPerSample3 = 8;
                u64XResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
                u64YResolution = (ulong)0x100000000 + (ulong)a_u32Resolution;
            }

            // Header...
            public ushort u16ByteOrder;
            public ushort u16Version;
            public uint u32OffsetFirstIFD;

            // First IFD...
            public ushort u16IFD;

            // Tags...
            public TiffTag tifftagNewSubFileType;
            public TiffTag tifftagSubFileType;
            public TiffTag tifftagImageWidth;
            public TiffTag tifftagImageLength;
            public TiffTag tifftagBitsPerSample;
            public TiffTag tifftagCompression;
            public TiffTag tifftagPhotometricInterpretation;
            public TiffTag tifftagStripOffsets;
            public TiffTag tifftagSamplesPerPixel;
            public TiffTag tifftagRowsPerStrip;
            public TiffTag tifftagStripByteCounts;
            public TiffTag tifftagXResolution;
            public TiffTag tifftagYResolution;
            public TiffTag tifftagResolutionUnit;

            // Footer...
            public uint u32NextIFD;
            public ushort u16XBitsPerSample1;
            public ushort u16XBitsPerSample2;
            public ushort u16XBitsPerSample3;
            public ulong u64XResolution;
            public ulong u64YResolution;
        }

        #endregion

    }
}
