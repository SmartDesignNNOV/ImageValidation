using System;
namespace ImageValidation
{
    public static class Factory
    {
        public static TensorflowClassifier CreateBlurClassifier()
            => new TensorflowClassifier("model5.tflite", "labels5.txt", false, true);

        public static TensorflowClassifier CreateObjectClassifier()
            => new TensorflowClassifier("object_labeler.tflite", "object_labeler_labelmap.txt", true, false);

        public static TensorflowDetector CreateObjectDetector()
            => new TensorflowDetector("object_detection.tflite", true, false);

        public static ReceiptDetector CreateReceiptDetector() => new ReceiptDetector();
    }
}
