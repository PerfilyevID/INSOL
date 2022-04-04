using Newtonsoft.Json;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace INSOL.ways
{
    public interface Calculation
    {
        int Run(Ray3d[] rays, Mesh[] meshes);
    }

    public class CheckWaySync : Calculation
    {
        public int Run(Ray3d[] rays, Mesh[] meshes)
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
        public int Run(Ray3d[] rays, Mesh[] meshes)
        {
            var mTasks = new Task<bool>[(rays.Length-1) * (meshes.Length-1)];
            int i = 0;
            for (int r = 0; r < rays.Length-1; r++)
            {
                for (int m = 0; m < meshes.Length-1; m++)
                {
                    mTasks[i++] = Task.Run(() =>
                    {
                        return Utills.IsHit(meshes[m], rays[r]);
                    });
                }
            }
            Task.WaitAll(mTasks);
            return mTasks.Where(t => t.Result == true).Count();
        }
    }


    public class CheckWayOptimized : Calculation
    {
        public int Run(Ray3d[] rays, Mesh[] meshes)
        {
            int result = 0;
            BoundingCircle[] boundingCircles = meshes.Select(m => new BoundingCircle(m.GetBoundingBox(true))).ToArray();

            Parallel.For(0, rays.Length, r =>
            {
                for(int m = 0; m < meshes.Length; m++)
                {
                    if(Utills.Intersects(rays[r], boundingCircles[m])) {
                        Point3f[][] triangles = Utills.GetTriangles(meshes[m]);
                        if (Utills.IsHit(meshes[m], rays[r]))
                        {
                            result++;
                            break;
                        }
                    }
                    
                }
            });

            Parallel.For(0, boundingCircles.Length, i =>
            {
                Point3f[][] triangles = Utills.GetTriangles(meshes[i]);
                foreach (var ray in rays.Where(r => Utills.Intersects(r, boundingCircles[i])))
                {
                    if (Utills.IsHit(meshes[i], ray))
                    {
                        result++;
                    }
                }
            });
            return result;
        }
    }


    public class CheckWayOpenCL : Calculation
    {
        private const int Slice = 5000;
        private static bool[] RunCircles(Ray3d[] rays, BoundingCircle[] boundingCircles)
        {
            var r = new ArraySegment<Ray3d>(rays, 0, rays.Length);
            var c = new ArraySegment<BoundingCircle>(boundingCircles, 0, boundingCircles.Length);
            return Program.OCL.RunCheckCircleIntersection(r, c);
        }

        private static bool[] RunTriangles(Ray3d[] _rays, Point3f[][] _faces)
        {
            var r = new ArraySegment<Ray3d>(_rays, 0, _rays.Length);
            var t = new ArraySegment<Point3f[]>(_faces, 0, _faces.Length);
            return Program.OCL.RunCheckIntersection(r, t);
        }

        public int Run(Ray3d[] rays, Mesh[] meshes)
        {
            BoundingCircle[] boundingCircles = meshes.Select(m => new BoundingCircle(m.GetBoundingBox(true))).ToArray();
            int x = rays.Length;
            int y = boundingCircles.Length;

            bool[] result = new bool[] { false };

            var _t_circles = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                result = RunCircles(rays, boundingCircles);
            });
            _t_circles.Start();
            while(_t_circles.ThreadState != System.Threading.ThreadState.Stopped) Thread.Sleep(1);

            List<Ray3d> _rays = new List<Ray3d>();
            List<Point3f[]> _faces = new List<Point3f[]>();

            for (int r = 0; r < x; r++)
            {
                bool add = false;
                int index;
                for (int c = 0; c < y; c++)
                {
                    index = r * y + c;
                    if (result[index])
                    {
                        add = true;
                        foreach (var face in meshes[c].Faces)
                        {
                            _faces.Add(new Point3f[3] {
                                meshes[c].Vertices[face.A],
                                meshes[c].Vertices[face.B],
                                meshes[c].Vertices[face.C]
                            });
                            if (face.IsQuad)
                            {
                                _faces.Add(new Point3f[3] {
                                    meshes[c].Vertices[face.A],
                                    meshes[c].Vertices[face.C],
                                    meshes[c].Vertices[face.D]
                                });
                            }
                        }
                    }
                }
                if(add) _rays.Add(rays[r]);
            }


            List<bool> resultCollector = new List<bool>();

            var _t_triangles = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                int size = _faces.Count;
                int MaxSize = 500;
                for (int i = 0; i < (int)Math.Ceiling((float)size / MaxSize); i++)
                {
                    int __offset = i * MaxSize;
                    int __size = (i + 1) * MaxSize > size ? size % MaxSize : MaxSize;
                    var r = new ArraySegment<Point3f[]>(_faces.ToArray(), __offset, __size);
                    resultCollector.AddRange(RunTriangles(_rays.ToArray(), r.ToArray()));
                }
            });
            _t_triangles.Start();
            while (_t_triangles.ThreadState != System.Threading.ThreadState.Stopped) Thread.Sleep(1);

            int resultRaysIntersected = 0;
            int n = 0;

            for (int r = 0; r < x; r++)
            {
                for (int c = 0; c < y; c++)
                {
                    int index = r * y + c;
                    if (result[index])
                    {
                        if (resultCollector[n++])
                        {
                            resultRaysIntersected++;
                            //break;
                        }
                    }
                }
            }
            return resultRaysIntersected;
        }
    }
}