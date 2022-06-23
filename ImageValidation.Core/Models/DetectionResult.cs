using System;
using System.Collections.Generic;
using System.Drawing;

namespace ImageValidation.Models
{
    public class DetectionResult
    {
        public IEnumerable<DetectedLocation> Locations { get; }

        public DetectionResult(IEnumerable<DetectedLocation> locations)
        {
            Locations = locations;
        }
    }

    public class DetectedLocation
    {
        public Rectangle Rectangle { get; }
        public float Probability { get; }

        public DetectedLocation(Rectangle rectangle, float probability)
        {
            Rectangle = rectangle;
            Probability = probability;
        }
    }
}
