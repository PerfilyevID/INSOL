using INSOL.ways;
using Newtonsoft.Json;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace INSOL
{
    public class Program
    {
        public static OpenCLProgram TKCircles = new OpenCLProgram(File.ReadAllText($@"source\circles.cl"), "circleIntersection");
        public static OpenCLProgram TKIntersection = new OpenCLProgram(File.ReadAllText($@"source\triangles.cl"), "computeTriangles");
        public static void Main(string[] args)
        {
            var meshesPath = File.ReadAllText(@"source\Meshes.json");
            var meshes = JsonConvert.DeserializeObject<Mesh[]>(meshesPath);
            var vectorsPath = File.ReadAllText($@"source\Vectors.json");
            var vectors = JsonConvert.DeserializeObject<Vector3d[]>(vectorsPath);
            var rays = vectors.Select(v => new Ray3d(new Point3d(-1079800, 4580400, 0), v)).ToArray();

            List<Calculation> checks = new List<Calculation>()
            {
                new CheckWayOpenCL()
            };
            foreach (var c in checks)
            {
                Console.WriteLine(c.GetType().Name);
                var watch = Stopwatch.StartNew();
                var count = c.Run(rays, meshes);
                watch.Stop();
                Console.WriteLine(c.GetType().Name + " _ " + count + $" :{watch.ElapsedMilliseconds}ms");
            }
            Console.WriteLine("Done...");
            Console.ReadKey();
        }
    }
}
