using Newtonsoft.Json;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace INSOL.ways
{
    public interface Calculation
    {
        int Run(Ray3d[] rays, Mesh[] meshes);
    }
    /*
    public class CheckWaySync : Calculation
    {
        public async Task<int> Run(Ray3d[] rays, Mesh[] meshes)
        {
            int value = 0;
            foreach (var ray in rays)
            {
                foreach (var mesh in meshes)
                {
                    if (Utills.IsHit(mesh, ray))
                    {
                        value++;
                        break;
                    }
                }
            }
            return value;
        }
    }

    public class CheckWayAsync : Calculation
    {
        public async Task<int> Run(Ray3d[] rays, Mesh[] meshes)
        {
            var mTasks = new List<Task<bool>>();
            foreach (var ray in rays)
            {
                foreach (var mesh in meshes)
                {
                    mTasks.Add(Task.Run(() =>
                    {
                        return Utills.IsHit(mesh, ray);
                    }));
                }
            }
            await Task.WhenAll(mTasks);
            return mTasks.Where(t => t.Result == true).Count();
        }
    }
    */
    /*
    public class CheckWayOptimized : CheckWay
    {
        public async Task<int> Run(Ray3d[] rays, Mesh[] meshes)
        {
            int result = 0;

            BoundingCircle[] boundingCircles = meshes.Select(m => new BoundingCircle(m.GetBoundingBox(true))).ToArray();

            var dict = Utills.Prepare(meshes);
            Parallel.ForEach(dict.Keys, circle =>
            {
                Point3f[][] triangles = Utills.GetTriangles(dict[circle]);
                foreach(var ray in rays.Where(r => Utills.Intersects(r, circle)))
                {
                    if(Utills.IsHit(dict[circle], ray))
                    {
                        result++;
                    }
                }
            });
            return result;
        }
    }
    */

    public class CheckWayOpenCL : Calculation
    {
        public int Run(Ray3d[] rays, Mesh[] meshes)
        {
            //OpenCLProgram OCL = new OpenCLProgram(File.ReadAllText($@"source\circles.cl"), "circleIntersection");

            BoundingCircle[] boundingCircles = meshes.Select(m => new BoundingCircle(m.GetBoundingBox(true))).ToArray();
            int x = rays.Length;
            int y = boundingCircles.Length;

            int size = x * y;
            float[] ox = new float[size];
            float[] oy = new float[size];
            float[] or = new float[size];
            float[] px = new float[size];
            float[] py = new float[size];
            float[] dx = new float[size];
            float[] dy = new float[size];

            for (int r = 0; r < x; r++)
            {
                int index = 0;
                for (int c = 0; c < y; c++)
                {
                    index = r * y + c;
                    ox[index] = (float)boundingCircles[c].Origin.X;
                    oy[index] = (float)boundingCircles[c].Origin.Y;
                    or[index] = (float)boundingCircles[c].Radius;
                    px[index] = (float)rays[r].Position.X;
                    py[index] = (float)rays[r].Position.Y;
                    dx[index] = (float)rays[r].Direction.X;
                    dy[index] = (float)rays[r].Direction.X;
                }
            }

            float[][] collection = new float[][] { ox, oy, or, px, py, dx, dy };

            bool[] result = Program.TKCircles.Run(collection);
            return 0;


            List<Ray3d> _rays = new List<Ray3d>();
            List<Point3f[]> _faces = new List<Point3f[]>();

            for (int r = 0; r < x; r++)
            {
                int index;
                for (int c = 0; c < y; c++)
                {
                    index = r * y + c;
                    if (result[index])
                    {
                        foreach (var face in meshes[c].Faces)
                        {
                            _rays.Add(rays[r]);
                            _faces.Add(new Point3f[3] { meshes[c].Vertices[face.A], meshes[c].Vertices[face.B], meshes[c].Vertices[face.C] });
                            if (face.IsQuad)
                            {
                                _rays.Add(rays[r]);
                                _faces.Add(new Point3f[3] { meshes[c].Vertices[face.A], meshes[c].Vertices[face.C], meshes[c].Vertices[face.D] });
                            }
                        }
                    }
                }
            }

            size = _rays.Count;

            float[] a1 = new float[size];
            float[] a2 = new float[size];
            float[] a3 = new float[size];
            float[] b1 = new float[size];
            float[] b2 = new float[size];
            float[] b3 = new float[size];
            float[] c1 = new float[size];
            float[] c2 = new float[size];
            float[] c3 = new float[size];
            float[] o1 = new float[size];
            float[] o2 = new float[size];
            float[] o3 = new float[size];
            float[] d1 = new float[size];
            float[] d2 = new float[size];
            float[] d3 = new float[size];

            for (int r = 0; r < size; r++)
            {
                a1[r] = _faces[r][0].X;
                a2[r] = _faces[r][0].Y;
                a3[r] = _faces[r][0].Z;
                b1[r] = _faces[r][1].X;
                b2[r] = _faces[r][1].Y;
                b3[r] = _faces[r][1].Z;
                c1[r] = _faces[r][2].X;
                c2[r] = _faces[r][2].Y;
                c3[r] = _faces[r][2].Z;
                o1[r] = (float)_rays[r].Position.X;
                o2[r] = (float)_rays[r].Position.Y;
                o3[r] = (float)_rays[r].Position.Z;
                d1[r] = (float)_rays[r].Direction.X;
                d2[r] = (float)_rays[r].Direction.Y;
                d3[r] = (float)_rays[r].Direction.Z;
            }
            return 0;
            collection = new float[][] { a1.ToArray(), a2.ToArray(), a3.ToArray(), b1.ToArray(), b2.ToArray(), b3.ToArray(), c1.ToArray(), c2.ToArray(), c3.ToArray(), o1.ToArray(), o2.ToArray(), o3.ToArray(), d1.ToArray(), d2.ToArray(), d3.ToArray()};
            result = Program.TKIntersection.Run(collection);
            return 0;
        }
    }
}