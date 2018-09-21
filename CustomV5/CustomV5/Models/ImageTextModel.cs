using System;
using System.Collections.Generic;
using System.Text;

namespace CustomV5.Models
{
    class ImageTextModel
    {
        public class TextObject
        {
            public string language { get; set; }
            public string orientation { get; set; }
            public float textAngle { get; set; }
            public Region[] regions { get; set; }
        }

        public class Region
        {
            public string boundingBox { get; set; }
            public Line[] lines { get; set; }
        }

        public class Line
        {
            public string boundingBox { get; set; }
            public Word[] words { get; set; }
        }

        public class Word
        {
            public string boundingBox { get; set; }
            public string text { get; set; }
        }
    }
}
