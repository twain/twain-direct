// Helpers...
using System;
using System.IO;
using TwainDirectSupport;

namespace TwainDirectOnTwain
{
    public sealed class TestPdfRaster
    {
        /// <summary>
        /// Init stuff...
        /// </summary>
        public TestPdfRaster()
        {
        }

        /// <summary>
        /// Test the code...
        /// </summary>
        /// <returns></returns>
        public bool Test()
        {
            BinaryWriter binarywriter;
            PdfRaster pdfraster = new PdfRaster();
            PdfRaster.t_OS os;
            byte[] bitonalData = new byte[((850+7)/8) * 1100];
            string OUTPUT_FILENAME = "raster.pdf";

            byte[] _imdata = new byte[]
            {
	            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
	            0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
	            0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40,
	            0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x60, 0x60,
	            0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
	            0xa0, 0xa0, 0xa0, 0xa0, 0xa0, 0xa0, 0xa0, 0xa0,
	            0xc0, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0,
	            0xe0, 0xe0, 0xe0, 0xe0, 0xe0, 0xe0, 0xe0, 0xe0,
	            0xc0, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0,
	            0xa0, 0xa0, 0xa0, 0xa0, 0xa0, 0xa0, 0xa0, 0xa0,
	            0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
            };

            // Create the file...
            binarywriter = new BinaryWriter(File.Create(OUTPUT_FILENAME));
	        if (binarywriter == null)
            {
		        TWAINWorkingGroup.Log.Error("unable to open %s for writing " + OUTPUT_FILENAME);
		        return (false);
	        }

            // Set up our worker functions...
            os = new PdfRaster.t_OS();
	        os.alloc = null;
	        os.free = null;
	        os.memset = null;
	        os.memclear = null;
	        os.allocsys = PdfRaster.pd_alloc_sys_new(os);
	        os.writeout = myOutputWriter;
            os.writeoutcookie = binarywriter;

            // Construct a raster PDF encoder
            //object enc = pdfraster.pd_raster_encoder_create(PdfRaster.PdfRasterConst.PDFRAS_API_LEVEL, os);
            object enc = pdfraster.pd_raster_encoder_create(PdfRasterWriter.PdfRasterConst.PDFRASWR_API_LEVEL, os);//gusb
            PdfRaster.pd_raster_set_creator(enc, "raster_encoder_demo 1.0");

	        // First page - 4" x 5.5" at 2 DPI
            PdfRaster.pd_raster_set_resolution(enc, 2.0, 2.0);
            // start a new page
            // pdfraster.pd_raster_encoder_start_page(enc, PdfRaster.RasterPixelFormat.PDFRAS_GRAYSCALE, PdfRaster.RasterCompression.PDFRAS_UNCOMPRESSED, 8);
            pdfraster.pd_raster_encoder_start_page(enc, PdfRasterWriter.RasterPixelFormat.PDFRASWR_GRAYSCALE, PdfRasterWriter.RasterCompression.PDFRASWR_UNCOMPRESSED, 8);//gusb
            // write a strip of raster data to the current page
            // 11 rows high
            pdfraster.pd_raster_encoder_write_strip(enc, 11, _imdata, 0, (UInt32)_imdata.Length);
	        // the page is done
	        pdfraster.pd_raster_encoder_end_page(enc);

	        // Next page: bitonal 8.5 x 11 at 100 DPI with a light dotted grid
	        // generate page data
	        for (int i = 0; i < bitonalData.Length; i++)
            {
		        int y = (i / 107);
		        int b = (i % 107);
		        if ((y % 100) == 0)
                {
			        bitonalData[i] = 0xAA;
		        }
		        else if (((b % 12) == 0) && ((y & 1) != 0))
                {
			        bitonalData[i] = 0x7F;
		        }
		        else
                {
			        bitonalData[i] = 0xff;
		        }
	        }
            PdfRaster.pd_raster_set_resolution(enc, 100.0, 100.0);
            // pdfraster.pd_raster_encoder_start_page(enc, PdfRaster.RasterPixelFormat.PDFRAS_BITONAL, PdfRaster.RasterCompression.PDFRAS_UNCOMPRESSED, 850);
            pdfraster.pd_raster_encoder_start_page(enc, PdfRasterWriter.RasterPixelFormat.PDFRASWR_BITONAL, PdfRasterWriter.RasterCompression.PDFRASWR_UNCOMPRESSED, 850);//gusb
            pdfraster.pd_raster_encoder_write_strip(enc, 1100, bitonalData, 0, (UInt32)bitonalData.Length);
	        pdfraster.pd_raster_encoder_end_page(enc);

	        // Third page: color 3.5" x 2" 50 DPI
            int stride = 3;
            byte[] colorData = new byte[175 * 100 * stride];
            for (int i = 0; i < colorData.Length; i += stride)
            {
                int y = ((i / stride) / 175);
                int x = ((i / stride) % 175);
                colorData[i + 0] = (byte)((i / stride) % 255);
		        colorData[i + 1] = (byte)((x + y) % 255);
		        colorData[i + 2] = (byte)((x - y + 9999) % 255);
	        }
            PdfRaster.pd_raster_set_resolution(enc, 50.0, 50.0);
            // pdfraster.pd_raster_encoder_start_page(enc, PdfRaster.RasterPixelFormat.PDFRAS_RGB, PdfRaster.RasterCompression.PDFRAS_UNCOMPRESSED, 175);
            pdfraster.pd_raster_encoder_start_page(enc, PdfRasterWriter.RasterPixelFormat.PDFRASWR_RGB, PdfRasterWriter.RasterCompression.PDFRASWR_UNCOMPRESSED, 175);//gusb
            pdfraster.pd_raster_encoder_write_strip(enc, 100, colorData, 0, (UInt32)colorData.Length);
	        pdfraster.pd_raster_encoder_end_page(enc);

	        // the document is complete
	        pdfraster.pd_raster_encoder_end_document(enc);

	        // clean up
	        binarywriter.Flush();
            binarywriter.Close();
	        pdfraster.pd_raster_encoder_destroy(enc);

            // All done...
            return (true);
        }

        private long myOutputWriter(byte[] data, long offset, long len, object cookie)
        {
	        BinaryWriter binarywriter = (BinaryWriter)cookie;
	        if ((data == null) || (len == 0))
            {
		        return (0);
            }
            binarywriter.Write(data,(int)offset,(int)len);
	        return (len);
        }
    }
}
