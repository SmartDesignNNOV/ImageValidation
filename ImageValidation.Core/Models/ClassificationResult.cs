using System;
using System.Collections.Generic;

namespace ImageValidation.Models
{
    public class ClassificationResult
    {
        public IReadOnlyList<Classification> Predictions { get; }

        public ClassificationResult(IReadOnlyList<Classification> predictions)
        {
            Predictions = predictions;
        }
    }

    public class Classification
    {
        public float Probability { get; }
        public string TagName { get; }

        public Classification(string tagName, float probability)
        {
            TagName = tagName;
            Probability = probability;
        }
    }
}
