using System;
namespace ImageValidation
{
    public static class Platform
    {
        public static void Init()
        {
            _ = typeof(TensorflowClassifier);
            _ = typeof(TensorflowDetector);
        }
    }
}
