﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Research.GraphBasedShapePrior.Util;

namespace Research.GraphBasedShapePrior
{
    [DataContract]
    public class Shape
    {
        [DataMember]
        private readonly List<Vector> vertexPositions;

        [DataMember]
        private readonly List<double> edgeWidths;

        private ExposableCollection<Vector> vertexPositionsExposable;

        private ExposableCollection<double> edgeWidthsExposable;

        public Shape(ShapeStructure structure, IEnumerable<Vector> vertexPositions, IEnumerable<double> edgeWidths)
        {
            if (structure == null)
                throw new ArgumentNullException("structure");
            if (vertexPositions == null)
                throw new ArgumentNullException("vertexPositions");
            if (edgeWidths == null)
                throw new ArgumentNullException("edgeWidths");

            this.Structure = structure;
            this.vertexPositions = new List<Vector>(vertexPositions);
            this.edgeWidths = new List<double>(edgeWidths);

            if (this.vertexPositions.Count != structure.VertexCount)
                throw new ArgumentException("Wrong number of vertex positions given.", "vertexPositions");
            if (this.edgeWidths.Count != structure.Edges.Count)
                throw new ArgumentException("Wrong number of edge widths given.", "edgeWidths");

            this.InitExposableCollections();
        }

        public static Shape LoadFromFile(string fileName)
        {
            return Helper.LoadFromFile<Shape>(fileName);
        }

        public void SaveToFile(string fileName)
        {
            Helper.SaveToFile(fileName, this);
        }

        public Shape FitToSize(double width, double height)
        {
            Vector min = new Vector(Double.PositiveInfinity, Double.PositiveInfinity);
            Vector max = new Vector(Double.NegativeInfinity, Double.NegativeInfinity);
            foreach (Vector vertexPosition in vertexPositions)
            {
                min.X = Math.Min(min.X, vertexPosition.X);
                min.Y = Math.Min(min.Y, vertexPosition.Y);
                max.X = Math.Max(max.X, vertexPosition.X);
                max.Y = Math.Max(max.Y, vertexPosition.Y);
            }

            double widthRatio = width / (max.X - min.X);
            double heightRatio = height / (max.Y - min.Y);
            double scale = Math.Min(widthRatio, heightRatio);

            return this.Scale(scale, min);
        }

        public Shape Scale(double scale, Vector origin)
        {
            IEnumerable<Vector> fittedVertexPositions = this.vertexPositions.Select(pos => (pos - origin) * scale);
            IEnumerable<double> fittedEdgeWidths = this.edgeWidths.Select(w => w * scale);
            return new Shape(this.Structure, fittedVertexPositions, fittedEdgeWidths);
        }

        public ShapeLengthAngleRepresentation GetLengthAngleRepresentation()
        {
            List<double> lengths = new List<double>(this.Structure.Edges.Count);
            List<double> angles = new List<double>(this.Structure.Edges.Count);
            for (int i = 0; i < this.Structure.Edges.Count; ++i)
            {
                Vector edgeVec = this.GetEdgeVector(i);
                lengths.Add(edgeVec.Length);
                angles.Add(Vector.AngleBetween(Vector.UnitX, edgeVec));
            }

            return new ShapeLengthAngleRepresentation(this.Structure, this.VertexPositions[this.Structure.Edges[0].Index1], lengths, angles, this.EdgeWidths);
        }

        [DataMember]
        public ShapeStructure Structure { get; private set; }

        public ExposableCollection<Vector> VertexPositions
        {
            get { return this.vertexPositionsExposable; }
        }

        public ExposableCollection<double> EdgeWidths
        {
            get { return this.edgeWidthsExposable; }
        }

        public Vector GetEdgeVector(int edgeIndex)
        {
            ShapeEdge edge = this.Structure.Edges[edgeIndex];
            return this.VertexPositions[edge.Index2] - this.VertexPositions[edge.Index1];
        }

        public Shape Clone()
        {
            return new Shape(this.Structure, this.vertexPositions, this.edgeWidths);
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext streamingContext)
        {
            this.InitExposableCollections();
        }

        private void InitExposableCollections()
        {
            this.vertexPositionsExposable = new ExposableCollection<Vector>(this.vertexPositions);
            this.edgeWidthsExposable = new ExposableCollection<double>(this.edgeWidths);
        }
    }
}
