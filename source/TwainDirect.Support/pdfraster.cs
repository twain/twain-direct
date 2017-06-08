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
using System.IO;
using System.Runtime.InteropServices;

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
