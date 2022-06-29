using Android.Graphics;
using Java.Nio;

namespace ImageValidation
{
    public static class ByteBufferExtension
    {
        private const int FloatSize = 4;

        public static ByteBuffer CreateQuantizedByteBuffer(Bitmap bitmap, int width, int height, int pixelSize)
        {
            var modelInputSize = height * width * pixelSize;

            var resizedBitmap = Bitmap.CreateScaledBitmap(bitmap, width, height, true);

            var byteBuffer = ByteBuffer.AllocateDirect(modelInputSize);
            byteBuffer.Order(ByteOrder.NativeOrder());

            var pixels = new int[width * height];
            resizedBitmap.GetPixels(pixels, 0, resizedBitmap.Width, 0, 0, resizedBitmap.Width, resizedBitmap.Height);
            var pixel = 0;

            var bytes = new byte[modelInputSize];

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    var pixelVal = pixels[pixel];
                    bytes[pixel * pixelSize] = (byte)(pixelVal >> 16 & 0xFF);
                    bytes[pixel * pixelSize + 1] = (byte)(pixelVal >> 8 & 0xFF);
                    bytes[pixel * pixelSize + 2] = (byte)(pixelVal & 0xFF);
                    pixel++;
                }
            }

            byteBuffer.Put(bytes);

            if (resizedBitmap != bitmap)
            {
                resizedBitmap.Recycle();
            }

            return byteBuffer;
        }

        public static ByteBuffer CreateByteBuffer(Bitmap bitmap, int width, int height, int pixelSize)
        {
            var modelInputSize = FloatSize * height * width * pixelSize;

            var resizedBitmap = Bitmap.CreateScaledBitmap(bitmap, width, height, true);

            var byteBuffer = ByteBuffer.AllocateDirect(modelInputSize);
            byteBuffer.Order(ByteOrder.NativeOrder());

            var pixels = new int[width * height];
            resizedBitmap.GetPixels(pixels, 0, resizedBitmap.Width, 0, 0, resizedBitmap.Width, resizedBitmap.Height);

            var pixel = 0;

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    var pixelVal = pixels[pixel++];

                    byteBuffer.PutFloat((pixelVal >> 16 & 0xFF) / 255.0f);
                    byteBuffer.PutFloat((pixelVal >> 8 & 0xFF) / 255.0f);
                    byteBuffer.PutFloat((pixelVal & 0xFF) / 255.0f);
                }
            }

            if (resizedBitmap != bitmap)
            {
                resizedBitmap.Recycle();
            }

            return byteBuffer;
        }
    }
}
