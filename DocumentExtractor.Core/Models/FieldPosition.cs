using System;
using System.ComponentModel.DataAnnotations;

namespace DocumentExtractor.Core.Models
{
    /// <summary>
    /// Represents the position and size of a field within a document
    /// Used for highlighting fields in the UI and tracking extraction coordinates
    /// Essential for the hybrid AI architecture's visual feedback system
    /// </summary>
    public class FieldPosition
    {
        /// <summary>
        /// Primary key for the field position entity
        /// Required for Entity Framework
        /// </summary>
        [Key]
        public int Id { get; set; }
        /// <summary>
        /// X coordinate (horizontal position) in the document
        /// Typically measured in pixels from the left edge
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y coordinate (vertical position) in the document
        /// Typically measured in pixels from the top edge
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of the field area in pixels
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the field area in pixels
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Page number where this field is located (for multi-page documents)
        /// Defaults to 1 for single-page documents
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Default constructor
        /// </summary>
        public FieldPosition()
        {
        }

        /// <summary>
        /// Constructor for creating a field position with all coordinates
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="width">Width of the field</param>
        /// <param name="height">Height of the field</param>
        /// <param name="page">Page number (optional, defaults to 1)</param>
        public FieldPosition(int x, int y, int width, int height, int page = 1)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Page = page;
        }

        /// <summary>
        /// Create a FieldPosition from a bounding box string format
        /// Expected format: "x,y,width,height" or "x,y,width,height,page"
        /// </summary>
        /// <param name="boundingBox">Bounding box string</param>
        /// <returns>FieldPosition object or null if parsing fails</returns>
        public static FieldPosition? FromBoundingBox(string? boundingBox)
        {
            if (string.IsNullOrWhiteSpace(boundingBox))
                return null;

            var parts = boundingBox.Split(',');
            if (parts.Length < 4)
                return null;

            if (int.TryParse(parts[0], out int x) &&
                int.TryParse(parts[1], out int y) &&
                int.TryParse(parts[2], out int width) &&
                int.TryParse(parts[3], out int height))
            {
                int page = 1;
                if (parts.Length > 4)
                {
                    int.TryParse(parts[4], out page);
                }

                return new FieldPosition(x, y, width, height, page);
            }

            return null;
        }

        /// <summary>
        /// Convert this FieldPosition to a bounding box string
        /// Format: "x,y,width,height,page"
        /// </summary>
        /// <returns>Bounding box string representation</returns>
        public string ToBoundingBox()
        {
            return $"{X},{Y},{Width},{Height},{Page}";
        }

        /// <summary>
        /// Check if this position overlaps with another position
        /// Useful for detecting conflicting field extractions
        /// </summary>
        /// <param name="other">Other position to check against</param>
        /// <returns>True if positions overlap</returns>
        public bool OverlapsWith(FieldPosition other)
        {
            if (other == null || Page != other.Page)
                return false;

            return !(X + Width <= other.X || 
                     other.X + other.Width <= X || 
                     Y + Height <= other.Y || 
                     other.Y + other.Height <= Y);
        }

        /// <summary>
        /// Calculate the area of this field position
        /// </summary>
        /// <returns>Area in square pixels</returns>
        public int GetArea()
        {
            return Width * Height;
        }

        /// <summary>
        /// Check if this position contains a point
        /// </summary>
        /// <param name="pointX">X coordinate of the point</param>
        /// <param name="pointY">Y coordinate of the point</param>
        /// <returns>True if the point is within this position</returns>
        public bool Contains(int pointX, int pointY)
        {
            return pointX >= X && pointX <= X + Width &&
                   pointY >= Y && pointY <= Y + Height;
        }

        /// <summary>
        /// Calculate the distance from the center of this position to another position's center
        /// </summary>
        /// <param name="other">Other position</param>
        /// <returns>Distance in pixels</returns>
        public double DistanceTo(FieldPosition other)
        {
            if (other == null)
                return double.MaxValue;

            int centerX1 = X + Width / 2;
            int centerY1 = Y + Height / 2;
            int centerX2 = other.X + other.Width / 2;
            int centerY2 = other.Y + other.Height / 2;

            return Math.Sqrt(Math.Pow(centerX2 - centerX1, 2) + Math.Pow(centerY2 - centerY1, 2));
        }

        /// <summary>
        /// Create a copy of this FieldPosition
        /// </summary>
        /// <returns>New FieldPosition with same coordinates</returns>
        public FieldPosition Clone()
        {
            return new FieldPosition(X, Y, Width, Height, Page);
        }

        /// <summary>
        /// Check if this position is valid (has positive dimensions)
        /// </summary>
        /// <returns>True if position is valid</returns>
        public bool IsValid()
        {
            return Width > 0 && Height > 0 && X >= 0 && Y >= 0 && Page >= 1;
        }

        /// <summary>
        /// Override ToString for debugging and logging
        /// </summary>
        /// <returns>String representation of this position</returns>
        public override string ToString()
        {
            return $"FieldPosition(X={X}, Y={Y}, W={Width}, H={Height}, Page={Page})";
        }

        /// <summary>
        /// Override Equals for comparison
        /// </summary>
        /// <param name="obj">Object to compare with</param>
        /// <returns>True if positions are equal</returns>
        public override bool Equals(object? obj)
        {
            if (obj is FieldPosition other)
            {
                return X == other.X && Y == other.Y && 
                       Width == other.Width && Height == other.Height && 
                       Page == other.Page;
            }
            return false;
        }

        /// <summary>
        /// Override GetHashCode for use in collections
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Width, Height, Page);
        }
    }
}