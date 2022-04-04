// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace INSOL.ways
{
    public struct BoundingCircle
    {
        public BoundingCircle(BoundingBox box)
        {
            Origin = new Vector2d(box.Center.X, box.Center.Y);
            Radius = box.Diagonal.Length / 2;
        }
        public Vector2d Origin { get; }
        public double Radius { get; }
    }
    public static class Utills
    {
        
        public static bool Intersects(Ray3d ray, BoundingCircle circle)
        {
            var p = ray.Position; 
            var d = ray.Direction;
            Vector2d p0 = new Vector2d(p.X, p.Y);
            Vector2d d0 = new Vector2d(d.X, d.Y);
            Vector2d d1 = circle.Origin - p0;
            double angle = Math.Atan2(d1.Y, d1.X) - Math.Atan2(d0.Y, d0.X);
            double distance = Vector2d.Subtract(p0, circle.Origin).Length;
            double touch = Math.Sin(angle) * distance;
            return touch <= circle.Radius;
        }

        public static Point3f[][] GetTriangles(Mesh mesh)
        {
            List<Point3f[]> triangles = new List<Point3f[]>();
            foreach (var face in mesh.Faces)
            {
                try
                {
                    var pts = new Point3f[3]
                                    {
                    mesh.Vertices[face.A],
                    mesh.Vertices[face.B],
                    mesh.Vertices[face.C],
                                    };
                    triangles.Add(pts);
                    if (face.IsQuad)
                    {
                        pts = new Point3f[3]
                        {
                        mesh.Vertices[face.A],
                        mesh.Vertices[face.C],
                        mesh.Vertices[face.D]
                        };
                        triangles.Add(pts);
                    }
                }
                catch (Exception) { }
            }
            return triangles.ToArray();
        }

        public static bool IsHit(Mesh mesh, Ray3d ray)
        {
            bool isHit = false;

            var options = new ParallelOptions();
            Parallel.ForEach(mesh.Faces, options, (MeshFace face, ParallelLoopState loop) =>
            {
                var pts = new Point3f[3]
                {
                    mesh.Vertices[face.A],
                    mesh.Vertices[face.B],
                    mesh.Vertices[face.C],
                };

                var result = IntersectRayTriangle(ray, pts[0], pts[1], pts[2]);
                if (!float.IsNaN(result))
                {
                    isHit = true;
                    loop.Stop();
                }

                if (face.IsQuad)
                {
                    var secondResult = IntersectRayTriangle(ray, pts[0], pts[2], mesh.Vertices[face.D]);
                    if (!float.IsNaN(secondResult))
                    {
                        isHit = true;
                        loop.Stop();
                    }
                }
            });
            return isHit;
        }

        public static float IntersectRayTriangle(Ray3d ray, Point3f v0, Point3f v1, Point3f v2)
        {
            const float kEpsilon = 0.000001f;

            Vector3d e1 = v1 - v0;
            Vector3d e2 = v2 - v0;

            Vector3d h = Vector3d.CrossProduct(ray.Direction, e2);
            double a = e1 * h;
            if ((a > -kEpsilon) && (a < kEpsilon))
            {
                return float.NaN;
            }

            float f = 1.0f / (float)a;

            Vector3d s = ray.Position - v0;
            float u = f * (float)(s * h);
            if ((u < 0.0f) || (u > 1.0f))
            {
                return float.NaN;
            }

            Vector3d q = Vector3d.CrossProduct(s, e1);
            float v = f * (float)(ray.Direction * q);
            if ((v < 0.0f) || (u + v > 1.0f))
            {
                return float.NaN;
            }

            float t = f * (float)(e2 * q);
            if (t > kEpsilon)
            {
                return t;
            }
            else
            {
                return float.NaN;
            }
        }
    }
}
