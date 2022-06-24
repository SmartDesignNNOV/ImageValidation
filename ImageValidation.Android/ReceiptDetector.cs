using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Android.Graphics;

namespace ImageValidation
{
    public class ReceiptDetector
    {
        private readonly TensorflowClassifier _classifier;
        private readonly TensorflowClassifier _blurClassifier;
        private readonly TensorflowDetector _detector;

        public ReceiptDetector()
        {
            _classifier = Factory.CreateObjectClassifier();
            _blurClassifier = Factory.CreateBlurClassifier();
            _detector = Factory.CreateObjectDetector();
        }

        public async Task<(float, float)> GetReceiptProbability(Stream input)
        {
            var locations = await _detector.Detect(input);

            List<(float, float)> result = new List<(float, float)>();

            foreach (var location in locations.Locations)
            {
                input.Seek(0, SeekOrigin.Begin);
                Bitmap image = await BitmapFactory.DecodeStreamAsync(input);

                System.Drawing.Rectangle rect = location.Rectangle;

                var croppedImage = Bitmap.CreateBitmap(image, rect.X, rect.Y, rect.Width, rect.Height);
                image.Recycle();

                MemoryStream stream = new MemoryStream();
                croppedImage.Compress(Bitmap.CompressFormat.Jpeg, 60, stream);

                stream.Seek(0, SeekOrigin.Begin);
                var classifierResult = await _classifier.Classify(stream);

                stream.Dispose();

                var sampleOfImage = Bitmap.CreateBitmap(croppedImage,
                    (int)(croppedImage.Width / 2.0f),
                    (int)(croppedImage.Height / 2.0f),
                    Math.Min(300, (int)(croppedImage.Width / 2.0f - 1)), Math.Min(300, (int)(croppedImage.Height / 2.0f - 1)));
                croppedImage.Recycle();

                stream = new MemoryStream();
                sampleOfImage.Compress(Bitmap.CompressFormat.Jpeg, 60, stream);

                stream.Seek(0, SeekOrigin.Begin);
                var blurClassifierResult = await _blurClassifier.Classify(stream);

                stream.Dispose();
                sampleOfImage.Recycle();

                result.Add((classifierResult.Predictions[325].Probability, blurClassifierResult.Predictions[0].Probability));
            }

            return result.Any() ? result.OrderByDescending(i => i.Item1).First() : (0.0f, 0.0f);
        }
    }
}
