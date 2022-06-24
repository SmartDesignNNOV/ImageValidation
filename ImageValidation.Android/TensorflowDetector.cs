using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.App;
using Android.Graphics;
using Java.IO;
using Java.Nio;
using Java.Nio.Channels;
using ImageValidation.Models;

namespace ImageValidation
{
    public class TensorflowDetector
    {
        private const int FloatSize = 4;
        private const int PixelSize = 3;

        private readonly string _tfliteModelAssetName;

        private readonly bool _quantized;
        private readonly bool _memoryMappedModel;

        private Xamarin.TensorFlow.Lite.Interpreter _interpreter;

        public TensorflowDetector(string tfliteModelAssetName, bool quantized, bool memoryMappedModel)
        {
            _tfliteModelAssetName = tfliteModelAssetName;

            _memoryMappedModel = memoryMappedModel;
            _quantized = quantized;
        }

        public Task<DetectionResult> Detect(Stream stream)
        {
            return Task.Run(() =>
            {
                var bitmap = BitmapFactory.DecodeStream(stream);
                try
                {
                    return PerformDetect(bitmap);
                }
                finally
                {
                    bitmap?.Recycle();
                }
            });
        }

        private DetectionResult PerformDetect(Bitmap bitmap)
        {
            LoadModel();

            var tensor = _interpreter.GetInputTensor(0);

            var shape = tensor.Shape();

            var width = shape[1];
            var height = shape[2];

            var originalWidth = bitmap.Width;
            var originalHeight = bitmap.Height;

            var byteBuffer = GetPhotoAsByteBuffer(bitmap, width, height);

            // Output boxes.
            Dictionary<Java.Lang.Integer, Java.Lang.Object> outputMap = new Dictionary<Java.Lang.Integer, Java.Lang.Object>()
            {
                // Detection boxes
                { Java.Lang.Integer.ValueOf(0), Java.Lang.Object.FromArray(CreateJagged(1, 100, 4)) },
                // Detection scores
                { Java.Lang.Integer.ValueOf(2), Java.Lang.Object.FromArray(CreateJagged(1, 100)) }
            };

            // Run the model.
            _interpreter.RunForMultipleInputsOutputs(new ByteBuffer[] { byteBuffer }, outputMap);

            var detectionBoxes = outputMap[Java.Lang.Integer.ValueOf(0)].ToArray<float[][]>();
            var detectionScores = outputMap[Java.Lang.Integer.ValueOf(2)].ToArray<float[]>();

            List<DetectedLocation> locations = new List<DetectedLocation>();

            var countOfInterestLocations = detectionScores[0].Count(v => v > 0.2);
            if (countOfInterestLocations > 0)
            {
                Func<float, float> minMax = (float v) => Math.Min(1.0f, Math.Max(0, v));

                for (var i = 0; i < countOfInterestLocations; i++)
                {
                    var boxPoints = detectionBoxes[0][i];
                    var boxScore = detectionScores[0][i];
                    var rect = new System.Drawing.Rectangle(
                        (int)(minMax(boxPoints[1]) * originalWidth),
                        (int)(minMax(boxPoints[0]) * originalHeight),
                        (int)(minMax(boxPoints[3]) * originalWidth) - (int)(minMax(boxPoints[1]) * originalWidth),
                        (int)(minMax(boxPoints[2]) * originalHeight) - (int)(minMax(boxPoints[0]) * originalHeight));
                    locations.Add(new DetectedLocation(rect, boxScore));
                }
            }

            return new DetectionResult(locations);
        }

        private void LoadModel()
        {
            if (_interpreter == null)
            {
                if (_memoryMappedModel)
                {
                    var mappedByteBuffer = GetModelAsMappedByteBuffer();
                    _interpreter = new Xamarin.TensorFlow.Lite.Interpreter(mappedByteBuffer);
                }
                else
                {
                    var modelFile = GetModeAsFile();
                    _interpreter = new Xamarin.TensorFlow.Lite.Interpreter(modelFile);
                }
            }
        }

        private static float[][] CreateJagged(int lay1, int lay2)
        {
            var arr = new float[lay1][];

            for (int i = 0; i < lay1; i++)
            {
                arr[i] = new float[lay2];
            }
            return arr;
        }

        private static float[][][] CreateJagged(int lay1, int lay2, int lay3)
        {
            var arr = new float[lay1][][];

            for (int i = 0; i < lay1; i++)
            {
                arr[i] = CreateJagged(lay2, lay3);
            }
            return arr;
        }

        private Java.IO.File GetModeAsFile()
        {
            Java.IO.File f = new Java.IO.File(Application.Context.CacheDir + _tfliteModelAssetName);
            if (!f.Exists())
            {
                try
                {
                    using var fis = Application.Context.Assets.Open(_tfliteModelAssetName);

                    using FileOutputStream fos = new FileOutputStream(f);

                    byte[] buffer = new byte[1024];
                    int length = 0;

                    while ((length = fis.Read(buffer)) > 0)
                    {
                        fos.Write(buffer, 0, length);
                    }

                    fos.Close();
                    fis.Close();

                }
                catch (Exception e) { throw; }
            }

            return f;
        }

        private MappedByteBuffer GetModelAsMappedByteBuffer()
        {
            var assetDescriptor = Application.Context.Assets.OpenFd(_tfliteModelAssetName);

            var inputStream = new FileInputStream(assetDescriptor.FileDescriptor);
            var mappedByteBuffer = inputStream.Channel.Map(FileChannel.MapMode.ReadOnly, assetDescriptor.StartOffset, assetDescriptor.DeclaredLength);

            return mappedByteBuffer;
        }

        private ByteBuffer GetPhotoAsByteBuffer(Bitmap bitmap, int width, int height)
        {
            var modelInputSize = _quantized ? (height * width * PixelSize) : (FloatSize * height * width * PixelSize);

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

                    if (_quantized)
                    {
                        byteBuffer.Put((sbyte)(pixelVal >> 16 & 0xFF));
                        byteBuffer.Put((sbyte)(pixelVal >> 8 & 0xFF));
                        byteBuffer.Put((sbyte)(pixelVal & 0xFF));
                    }
                    else
                    {
                        byteBuffer.PutFloat(pixelVal >> 16 & 0xFF);
                        byteBuffer.PutFloat(pixelVal >> 8 & 0xFF);
                        byteBuffer.PutFloat(pixelVal & 0xFF);
                    }
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
