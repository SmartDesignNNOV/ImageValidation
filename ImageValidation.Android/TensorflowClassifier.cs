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
    public class TensorflowClassifier
    {
        private const int PixelSize = 3;

        private readonly string _tfliteModelAssetName;
        private readonly string _labelsAssetName;

        private readonly bool _quantized;
        private readonly bool _memoryMappedModel;

        private Xamarin.TensorFlow.Lite.Interpreter _interpreter;

        public TensorflowClassifier(string tfliteModelAssetName, string labelsAssetName, bool quantized, bool memoryMappedModel)
        {
            _tfliteModelAssetName = tfliteModelAssetName;
            _labelsAssetName = labelsAssetName;

            _memoryMappedModel = memoryMappedModel;
            _quantized = quantized;
        }

        public Task<ClassificationResult> Classify(byte[] imageBytes)
        {
            return Task.Run(() =>
            {
                var bitmap = BitmapFactory.DecodeByteArray(imageBytes, 0, imageBytes.Length);
                try
                {
                    return PerformClassify(bitmap);
                }
                finally
                {
                    bitmap?.Recycle();
                }
            });
        }

        public Task<ClassificationResult> Classify(Stream stream)
        {
            return Task.Run(() =>
            {
                var bitmap = BitmapFactory.DecodeStream(stream);
                try
                {
                    return PerformClassify(bitmap);
                }
                finally
                {
                    bitmap?.Recycle();
                }
            });
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

        private ClassificationResult PerformClassify(Bitmap bitmap)
        {
            LoadModel();

            var tensor = _interpreter.GetInputTensor(0);

            var shape = tensor.Shape();

            var width = shape[1];
            var height = shape[2];

            var byteBuffer = _quantized
                ? ByteBufferExtension.CreateQuantizedByteBuffer(bitmap, width, height, PixelSize)
                : ByteBufferExtension.CreateByteBuffer(bitmap, width, height, PixelSize);

            var sr = new StreamReader(Application.Context.Assets.Open(_labelsAssetName));
            var labels = sr.ReadToEnd().Split('\n').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            var result = new List<Classification>();

            if (_quantized)
            {
                byte[][] outputLocations = new byte[1][] { new byte[labels.Count] };

                var outputs = Java.Lang.Object.FromArray(outputLocations);

                _interpreter.Run(byteBuffer, outputs);

                var classificationResult = outputs.ToArray<byte[]>();
                for (var i = 0; i < labels.Count; i++)
                {
                    var label = labels[i];
                    var prob = classificationResult[0][i] / 255.0f;
                    result.Add(new Classification(label, prob));
                }
            }
            else
            {
                float[][] outputLocations = new float[1][] { new float[labels.Count] };

                var outputs = Java.Lang.Object.FromArray(outputLocations);

                _interpreter.Run(byteBuffer, outputs);

                var classificationResult = outputs.ToArray<float[]>();

                for (var i = 0; i < labels.Count; i++)
                {
                    result.Add(new Classification(labels[i], classificationResult[0][i]));
                }
            }

            return new ClassificationResult(result);
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
    }
}
