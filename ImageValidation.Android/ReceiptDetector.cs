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
        private readonly TensorflowDetector _detector;

        public ReceiptDetector()
        {
            _classifier = Factory.CreateObjectClassifier();
            //_blurClassifier = Factory.CreateBlurClassifier();
            _detector = Factory.CreateObjectDetector();
        }

        public async Task<float> GetReceiptProbability(Stream input)
        {
            var locations = await _detector.Detect(input);

            List<float> result = new List<float>();

            foreach (var location in locations.Locations)
            {
                input.Seek(0, SeekOrigin.Begin);
                Bitmap image = await BitmapFactory.DecodeStreamAsync(input);

                System.Drawing.Rectangle rect = location.Rectangle;

                var croppedImage = Bitmap.CreateBitmap(image, rect.X, rect.Y, rect.Width, rect.Height);
                image.Recycle();

                MemoryStream stream = new MemoryStream();
                croppedImage.Compress(Bitmap.CompressFormat.Jpeg, 80, stream);

                croppedImage.Recycle();

                stream.Seek(0, SeekOrigin.Begin);
                var classifierResult = await _classifier.Classify(stream);

                stream.Dispose();

                result.Add(classifierResult.Predictions[325].Probability);
            }

            return result.Any() ? result.Max() : 0.0f;
        }
    }
}
