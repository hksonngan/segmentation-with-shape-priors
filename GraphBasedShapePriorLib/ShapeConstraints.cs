﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace Research.GraphBasedShapePrior
{
    public class ShapeConstraints
    {
        private List<VertexConstraints> vertexConstraints;
        private List<EdgeConstraints> edgeConstraints;

        public ShapeModel ShapeModel { get; private set; }

        private ShapeConstraints()
        {
        }

        private ShapeConstraints(ShapeConstraints other)
            : this()
        {
            this.vertexConstraints = new List<VertexConstraints>(other.vertexConstraints);
            this.edgeConstraints = new List<EdgeConstraints>(other.edgeConstraints);
            this.ShapeModel = other.ShapeModel;
            this.MaxCoordFreedom = other.MaxCoordFreedom;
            this.MaxWidthFreedom = other.MaxWidthFreedom;
        }

        public static ShapeConstraints CreateFromConstraints(
            ShapeModel model,
            IEnumerable<VertexConstraints> vertexConstraints,
            IEnumerable<EdgeConstraints> edgeConstraints,
            double maxCoordFreedom,
            double maxWidthFreedom)
        {
            if (model == null)
                throw new ArgumentNullException("model");
            if (vertexConstraints == null)
                throw new ArgumentNullException("vertexConstraints");
            if (edgeConstraints == null)
                throw new ArgumentNullException("edgeConstraints");
            if (maxCoordFreedom <= 0)
                throw new ArgumentOutOfRangeException("maxCoordFreedom", "Parameter value should be positive");
            if (maxWidthFreedom <= 0)
                throw new ArgumentOutOfRangeException("maxWidthFreedom", "Parameter value should be positive");
            
            ShapeConstraints result = new ShapeConstraints();
            result.ShapeModel = model;
            result.vertexConstraints = new List<VertexConstraints>(vertexConstraints);
            result.edgeConstraints = new List<EdgeConstraints>(edgeConstraints);
            result.MaxCoordFreedom = maxCoordFreedom;
            result.MaxWidthFreedom = maxWidthFreedom;

            if (result.vertexConstraints.Count != result.ShapeModel.VertexCount)
                throw new ArgumentException("Vertex constraint should be given for every vertex (and for every vertex only).", "vertexConstraints");
            if (result.edgeConstraints.Count != result.ShapeModel.Edges.Count)
                throw new ArgumentException("Edge constraint should be given for every edge (and for every vertex only).", "edgeConstraints");

            return result;
        }

        public static ShapeConstraints CreateFromShape(Shape shape)
        {
            IEnumerable<VertexConstraints> vertexConstraints =
                shape.VertexPositions.Select(vertex => new VertexConstraints(vertex));
            IEnumerable<EdgeConstraints> edgeConstraints =
                shape.EdgeWidths.Select(width => new EdgeConstraints(width));
            return CreateFromConstraints(shape.Model, vertexConstraints, edgeConstraints, 1e-6, 1e-6);
        }

        public static ShapeConstraints CreateFromBounds(
            ShapeModel model,
            Vector coordMin,
            Vector coordMax,
            double minEdgeWidth,
            double maxEdgeWidth,
            double maxCoordFreedom,
            double maxWidthFreedom)
        {
            ShapeConstraints result = new ShapeConstraints();
            result.ShapeModel = model;
            result.vertexConstraints = new List<VertexConstraints>();
            result.edgeConstraints = new List<EdgeConstraints>();
            result.MaxCoordFreedom = maxCoordFreedom;
            result.MaxWidthFreedom = maxWidthFreedom;

            for (int i = 0; i < model.VertexCount; ++i)
                result.vertexConstraints.Add(new VertexConstraints(coordMin, coordMax));

            for (int i = 0; i < model.Edges.Count; ++i)
                result.edgeConstraints.Add(new EdgeConstraints(minEdgeWidth, maxEdgeWidth));

            return result;
        }

        public double MaxCoordFreedom { get; private set; }

        public double MaxWidthFreedom { get; private set; }

        public List<ShapeConstraints> SplitMostFree()
        {
            Debug.Assert(!this.CheckIfSatisfied());

            // Most violated vertex constraint
            int mostFreeVertexConstraint = -1;
            double maxVertexFreedom = 0;
            for (int i = 0; i < vertexConstraints.Count; ++i)
            {
                if (vertexConstraints[i].Freedom > this.MaxCoordFreedom &&
                    (mostFreeVertexConstraint == -1 || vertexConstraints[i].Freedom > maxVertexFreedom))
                {
                    mostFreeVertexConstraint = i;
                    maxVertexFreedom = vertexConstraints[i].Freedom;
                }
            }
            
            // Most violated edge constraint
            int mostFreeEdgeConstraint = -1;
            double maxEdgeFreedom = 0;
            for (int i = 0; i < edgeConstraints.Count; ++i)
            {
                if (edgeConstraints[i].Freedom > this.MaxWidthFreedom &&
                    (mostFreeEdgeConstraint == -1 || edgeConstraints[i].Freedom > maxEdgeFreedom))
                {
                    mostFreeEdgeConstraint = i;
                    maxEdgeFreedom = edgeConstraints[i].Freedom;
                }
            }

            bool splitEdgeConstraint = maxEdgeFreedom > maxVertexFreedom;
            List<ShapeConstraints> result = new List<ShapeConstraints>();
            if (splitEdgeConstraint)
            {
                List<EdgeConstraints> splittedEdgeConstraints =
                    this.edgeConstraints[mostFreeEdgeConstraint].Split();
                for (int i = 0; i < splittedEdgeConstraints.Count; ++i)
                {
                    ShapeConstraints newSet = new ShapeConstraints(this);
                    newSet.edgeConstraints[mostFreeEdgeConstraint] = splittedEdgeConstraints[i];
                    result.Add(newSet);
                }    
            }
            else
            {
                List<VertexConstraints> splittedVertexConstraints =
                    this.vertexConstraints[mostFreeVertexConstraint].Split();
                for (int i = 0; i < splittedVertexConstraints.Count; ++i)
                {
                    ShapeConstraints newSet = new ShapeConstraints(this);
                    newSet.vertexConstraints[mostFreeVertexConstraint] = splittedVertexConstraints[i];
                    result.Add(newSet);
                }    
            }

            return result;
        }

        public Polygon GetConvexHullForVertexPair(int vertex1, int vertex2)
        {
            List<Vector> points = new List<Vector>();
            points.AddRange(this.VertexConstraints[vertex1].Corners);
            points.AddRange(this.VertexConstraints[vertex2].Corners);
            Polygon convexHull = Polygon.ConvexHull(points);

            return convexHull;
        }

        public void DetermineEdgeLimits(
           int edgeIndex,
           out Range lengthRange,
           out Range angleRange)
        {
            ShapeEdge edge = this.ShapeModel.Edges[edgeIndex];
            VertexConstraints constraint1 = this.VertexConstraints[edge.Index1];
            VertexConstraints constraint2 = this.VertexConstraints[edge.Index2];

            Range xRange1 = new Range(constraint1.MinCoord.X, constraint1.MaxCoord.X);
            Range yRange1 = new Range(constraint1.MinCoord.Y, constraint1.MaxCoord.Y);
            Range xRange2 = new Range(constraint2.MinCoord.X, constraint2.MaxCoord.X);
            Range yRange2 = new Range(constraint2.MinCoord.Y, constraint2.MaxCoord.Y);

            bool xIntersection = xRange1.IntersectsWith(xRange2);
            bool yIntersection = yRange1.IntersectsWith(yRange2);

            double minLength = Double.PositiveInfinity, maxLength = 0;

            if (xIntersection && yIntersection)
            {
                // Special case: intersecting rectangles
                angleRange = new Range(-Math.PI, Math.PI);
                minLength = 0;
            }
            else
            {
                // Angle changes from PI to -PI when second constraint is to the left of the first one
                bool angleSignChanges = constraint1.MinCoord.X > constraint2.MaxCoord.X && yIntersection;
                
                double minAngle = angleSignChanges ? -Math.PI : Math.PI;
                double maxAngle = angleSignChanges ? Math.PI : -Math.PI;
                foreach (Vector point1 in constraint1.Corners)
                {
                    foreach (Vector point2 in constraint2.Corners)
                    {
                        double angle = Vector.AngleBetween(new Vector(1, 0), point2 - point1);
                        if (angleSignChanges)
                        {
                            if (angle < 0)
                                minAngle = Math.Max(minAngle, angle);
                            else
                                maxAngle = Math.Min(maxAngle, angle);
                        }
                        else
                        {
                            minAngle = Math.Min(minAngle, angle);
                            maxAngle = Math.Max(maxAngle, angle);    
                        }
                    }
                }                
                angleRange = new Range(minAngle, maxAngle, angleSignChanges);

                // One constraint is on top or on bottom of another
                if (xIntersection)
                {
                    // 1 on top of 2
                    if (constraint1.MinCoord.Y > constraint2.MaxCoord.Y)
                        minLength = Math.Min(minLength, constraint1.MinCoord.Y - constraint2.MaxCoord.Y);
                    // 2 on top of 1
                    else
                        minLength = Math.Min(minLength, constraint2.MinCoord.Y - constraint1.MaxCoord.Y);
                }
                else if (yIntersection)
                {
                    // 1 to the left of 2
                    if (constraint1.MaxCoord.X < constraint2.MinCoord.X)
                        minLength = Math.Min(minLength, constraint2.MinCoord.X - constraint1.MaxCoord.X);
                    // 2 to the left of 1
                    else
                        minLength = Math.Min(minLength, constraint1.MinCoord.X - constraint2.MaxCoord.X);
                }
            }

            foreach (Vector point1 in constraint1.Corners)
            {
                foreach (Vector point2 in constraint2.Corners)
                {
                    double length = (point1 - point2).Length;
                    minLength = Math.Min(minLength, length);
                    maxLength = Math.Max(maxLength, length);
                }
            }

            lengthRange = new Range(minLength, maxLength);
        }

        public bool CheckIfSatisfied()
        {
            for (int i = 0; i < vertexConstraints.Count; ++i)
                if (vertexConstraints[i].Freedom > this.MaxCoordFreedom)
                    return false;
            
            for (int i = 0; i < edgeConstraints.Count; ++i)
                if (edgeConstraints[i].Freedom > this.MaxWidthFreedom)
                    return false;
            
            return true;
        }

        public double GetMaxFreedom()
        {
            double maxViolation = vertexConstraints.Max(c => c.Freedom);
            maxViolation = Math.Max(maxViolation, edgeConstraints.Max(c => c.Freedom));
            return maxViolation;
        }

        public double GetFreedomSum()
        {
            double sum = 0;
            sum += vertexConstraints.Sum(c => c.Freedom);
            sum += edgeConstraints.Sum(c => c.Freedom);
            return sum;
        }

        public ReadOnlyCollection<VertexConstraints> VertexConstraints
        {
            get { return this.vertexConstraints.AsReadOnly(); }
        }

        public ReadOnlyCollection<EdgeConstraints> EdgeConstraints
        {
            get { return this.edgeConstraints.AsReadOnly(); }
        }

        public void Draw(Graphics graphics)
        {
            foreach (VertexConstraints vertexConstraint in vertexConstraints)
            {
                graphics.DrawRectangle(
                    Pens.Green,
                    (float)vertexConstraint.MinCoord.X,
                    (float)vertexConstraint.MinCoord.Y,
                    (float)(vertexConstraint.MaxCoord.X - vertexConstraint.MinCoord.X),
                    (float)(vertexConstraint.MaxCoord.Y - vertexConstraint.MinCoord.Y));
            }

            for (int i = 0; i < this.ShapeModel.Edges.Count; ++i)
            {
                ShapeEdge edge = this.ShapeModel.Edges[i];
                Vector point1 = this.vertexConstraints[edge.Index1].MiddleCoord;
                Vector point2 = this.vertexConstraints[edge.Index2].MiddleCoord;
                graphics.DrawLine(Pens.Orange, MathHelper.VecToPointF(point1), MathHelper.VecToPointF(point2));

                EdgeConstraints edgeConstraint = this.edgeConstraints[i];
                Vector diff = point2 - point1;
                Vector edgeNormal = (new Vector(diff.Y, -diff.X)).GetNormalized();
                Vector middle = point1 + 0.5 * diff;
                graphics.DrawLine(
                    Pens.Cyan,
                    MathHelper.VecToPointF(middle - edgeNormal * edgeConstraint.MaxWidth * 0.5),
                    MathHelper.VecToPointF(middle + edgeNormal * edgeConstraint.MaxWidth * 0.5));
                graphics.DrawLine(
                    Pens.Red,
                    MathHelper.VecToPointF(middle - edgeNormal * edgeConstraint.MinWidth * 0.5),
                    MathHelper.VecToPointF(middle + edgeNormal * edgeConstraint.MinWidth * 0.5));
            }
        }

        public ShapeConstraints GuessSolution()
        {
            List<VertexConstraints> collapsedVertexConstraints = this.vertexConstraints.Select(c => c.Collapse()).ToList();
            List<EdgeConstraints> collapsedEdgeConstraints = this.edgeConstraints.Select(c => c.Collapse()).ToList();
            return CreateFromConstraints(this.ShapeModel, collapsedVertexConstraints, collapsedEdgeConstraints, 1e-6, 1e-6);
        }
    }
}