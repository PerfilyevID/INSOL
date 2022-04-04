using OpenCL.Net;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace INSOL.ways
{
    public class OpenCLProgram
    {
        private Platform Platform;
        private Device Device;
        private Context Context;
        private OpenCL.Net.Program Program;
        private const int MaxRead = 30000;

        private void ContextNotify(string errInfo, byte[] data, IntPtr cb, IntPtr userData)
        {
            Console.WriteLine("OpenCL Notification: " + errInfo);
        }

        public OpenCLProgram(string code)
        {
            ErrorCode error;
            Platform = Cl.GetPlatformIDs(out error)[0];
            CheckErr(error, "GetPlatformIDs");

            Device = Cl.GetDeviceIDs(Platform, DeviceType.All, out error)[0];
            CheckErr(error, "GetDeviceIDs");
            
            Context = Cl.CreateContext(null, 1, new[] { Device }, ContextNotify, IntPtr.Zero, out error);
            CheckErr(error, "CreateContext");

            Program = Cl.CreateProgramWithSource(Context, 1, new[] { code }, null, out error);
            CheckErr(error, "CreateProgramWithSource");

            error = Cl.BuildProgram(Program, 1, new[] { Device }, string.Empty, null, IntPtr.Zero);
            CheckErr(error, "BuildProgram");
        }

        public bool[] RunCheckCircleIntersection(System.ArraySegment<Ray3d> _rays, System.ArraySegment<BoundingCircle> _circles)
        {
            ErrorCode _error;
            Event _event;

            int size = _rays.Count * _circles.Count;

            Kernel kernel = Cl.CreateKernel(Program, "circleIntersection", out _error);
            CheckErr(_error, "CreateBuffer");

            IMem<float>[] rayMemo = new IMem<float>[4];
            for (int i = 0; i < 4; i++)
            {
                rayMemo[i] = Cl.CreateBuffer<float>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, _rays.Count, out _error);
                CheckErr(_error, "CreateBuffer");
            }

            IMem<float>[] circleMemo = new IMem<float>[3];
            for (int i = 0; i < 3; i++)
            {
                circleMemo[i] = Cl.CreateBuffer<float>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, _circles.Count, out _error);
                CheckErr(_error, "CreateBuffer");
            }

            var metaBuffer = Cl.CreateBuffer<int>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, 2, out _error);
            CheckErr(_error, "CreateBuffer");

            var outputBuffer = Cl.CreateBuffer<bool>(Context, MemFlags.AllocHostPtr | MemFlags.WriteOnly, size, out _error);
            CheckErr(_error, "CreateBuffer");

            var cmdQueue = Cl.CreateCommandQueue(Context, Device, CommandQueueProperties.None, out _error);
            CheckErr(_error, "CreateCommandQueue");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[0], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Position.X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[1], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Position.Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[2], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Direction.X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[3], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Direction.Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, circleMemo[0], Bool.True, 0, _circles.Count, _circles.Select(x => (float)x.Origin.X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, circleMemo[1], Bool.True, 0, _circles.Count, _circles.Select(x => (float)x.Origin.Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, circleMemo[2], Bool.True, 0, _circles.Count, _circles.Select(x => (float)x.Radius).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<int>(cmdQueue, metaBuffer, Bool.True, 0, 2, new int[] { _circles.Count, _rays.Count }, 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            for (int i = 0; i < circleMemo.Length; i++)
            {
                _error = Cl.SetKernelArg<float>(kernel, (uint)i, circleMemo[i]);
                CheckErr(_error, "SetKernelArg");
            }

            for (int i = 0; i < rayMemo.Length; i++)
            {
                _error = Cl.SetKernelArg<float>(kernel, (uint)(i + circleMemo.Length), rayMemo[i]);
                CheckErr(_error, "SetKernelArg");
            }

            _error = Cl.SetKernelArg<int>(kernel, (uint)(circleMemo.Length + rayMemo.Length), metaBuffer);
            CheckErr(_error, "SetKernelArg");

            _error = Cl.SetKernelArg<bool>(kernel, (uint)(circleMemo.Length + rayMemo.Length + 1), outputBuffer);
            CheckErr(_error, "SetKernelArg");

            //Console.WriteLine($"Size: {size}");
            _error = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new IntPtr[] { (IntPtr)size, (IntPtr)1, (IntPtr)1 }, null, 0, null, out _event);
            CheckErr(_error, "EnqueueNDRangeKernel");

            _error = Cl.Finish(cmdQueue);
            CheckErr(_error, "Finish");

            List<bool> result = new List<bool>();

            for(int i = 0; i < (int)Math.Ceiling((float)size / MaxRead); i++)
            {
                int __offset = i * MaxRead;
                int __size = (i + 1) * MaxRead > size ? size % MaxRead : MaxRead;
                bool[] __arrC = new bool[__size];
                _error = Cl.EnqueueReadBuffer<bool>(cmdQueue, outputBuffer, Bool.True, 0, __size, __arrC, 0, null, out _event);
                CheckErr(_error, "EnqueueReadBuffer");

                result.AddRange(__arrC);

                GCHandle __pinnedOutputArray = GCHandle.Alloc(__arrC, GCHandleType.Pinned);
                __pinnedOutputArray.Free();
            }

            /*
            foreach (var i in result) Console.Write(i == true ? "+" : " ");
            bool[] arrC = new bool[size];
            _error = Cl.EnqueueReadBuffer<bool>(cmdQueue, outputBuffer, Bool.True, 0, size, arrC, 0, null, out _event);
            CheckErr(_error, "EnqueueReadBuffer");
            */

            _error = Cl.ReleaseKernel(kernel);
            CheckErr(_error, "ReleaseKernel");

            _error = Cl.ReleaseCommandQueue(cmdQueue);
            CheckErr(_error, "ReleaseCommandQueue");

            for (int i = 0; i < circleMemo.Length; i++)
            {
                _error = Cl.ReleaseMemObject(circleMemo[i]);
                CheckErr(_error, "ReleaseMemObject");
            }

            for (int i = 0; i < rayMemo.Length; i++)
            {
                _error = Cl.ReleaseMemObject(rayMemo[i]);
                CheckErr(_error, "ReleaseMemObject");
            }

            _error = Cl.ReleaseMemObject(outputBuffer);
            CheckErr(_error, "ReleaseMemObject");

            //GCHandle pinnedOutputArray = GCHandle.Alloc(arrC, GCHandleType.Pinned);
            //pinnedOutputArray.Free();

            return result.ToArray();
        }

        public bool[] RunCheckIntersection(System.ArraySegment<Ray3d> _rays, System.ArraySegment<Point3f[]> _triangles)
        {
            ErrorCode _error;
            Event _event;

            int size = _rays.Count * _triangles.Count;

            Kernel kernel = Cl.CreateKernel(Program, "computeTriangles", out _error);
            CheckErr(_error, "CreateBuffer");

            IMem<float>[] rayMemo = new IMem<float>[6];
            for (int i = 0; i < 6; i++)
            {
                rayMemo[i] = Cl.CreateBuffer<float>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, _rays.Count, out _error);
                CheckErr(_error, "CreateBuffer");
            }

            IMem<float>[] triangleMemo = new IMem<float>[9];
            for (int i = 0; i < 9; i++)
            {
                triangleMemo[i] = Cl.CreateBuffer<float>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, _triangles.Count, out _error);
                CheckErr(_error, "CreateBuffer");
            }

            var metaBuffer = Cl.CreateBuffer<int>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, 2, out _error);
            CheckErr(_error, "CreateBuffer");

            var outputBuffer = Cl.CreateBuffer<bool>(Context, MemFlags.AllocHostPtr | MemFlags.WriteOnly, size, out _error);
            CheckErr(_error, "CreateBuffer");

            var cmdQueue = Cl.CreateCommandQueue(Context, Device, CommandQueueProperties.None, out _error);
            CheckErr(_error, "CreateCommandQueue");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[0], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Position.X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[1], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Position.Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[2], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Position.Z).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[3], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Direction.X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[4], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Direction.Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, rayMemo[5], Bool.True, 0, _rays.Count, _rays.Select(x => (float)x.Direction.Z).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[0], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[0].X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[1], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[0].Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[2], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[0].Z).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[3], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[1].X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[4], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[1].Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[5], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[1].Z).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[6], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[2].X).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[7], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[2].Y).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, triangleMemo[8], Bool.True, 0, _triangles.Count, _triangles.Select(x => (float)x[2].Z).ToArray(), 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            _error = Cl.EnqueueWriteBuffer<int>(cmdQueue, metaBuffer, Bool.True, 0, 2, new int[] { _triangles.Count, _rays.Count }, 0, null, out _event);
            CheckErr(_error, "EnqueueWriteBuffer");

            for (int i = 0; i < triangleMemo.Length; i++)
            {
                _error = Cl.SetKernelArg<float>(kernel, (uint)i, triangleMemo[i]);
                CheckErr(_error, "SetKernelArg");
            }

            for (int i = 0; i < rayMemo.Length; i++)
            {
                _error = Cl.SetKernelArg<float>(kernel, (uint)(i + triangleMemo.Length), rayMemo[i]);
                CheckErr(_error, "SetKernelArg");
            }

            _error = Cl.SetKernelArg<int>(kernel, (uint)(triangleMemo.Length + rayMemo.Length), metaBuffer);
            CheckErr(_error, "SetKernelArg");

            _error = Cl.SetKernelArg<bool>(kernel, (uint)(triangleMemo.Length + rayMemo.Length + 1), outputBuffer);
            CheckErr(_error, "SetKernelArg");

            _error = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new IntPtr[] { (IntPtr)size, (IntPtr)1, (IntPtr)1 }, null, 0, null, out _event);
            CheckErr(_error, "EnqueueNDRangeKernel");

            _error = Cl.Finish(cmdQueue);
            CheckErr(_error, "Finish");

            List<bool> result = new List<bool>();

            for (int i = 0; i < (int)Math.Ceiling((float)size / MaxRead); i++)
            {
                int __offset = i * MaxRead;
                int __size = (i + 1) * MaxRead > size ? size % MaxRead : MaxRead;
                bool[] __arrC = new bool[__size];
                _error = Cl.EnqueueReadBuffer<bool>(cmdQueue, outputBuffer, Bool.True, 0, __size, __arrC, 0, null, out _event);
                CheckErr(_error, "EnqueueReadBuffer");

                result.AddRange(__arrC);

                GCHandle __pinnedOutputArray = GCHandle.Alloc(__arrC, GCHandleType.Pinned);
                __pinnedOutputArray.Free();
            }

            /*
            bool[] arrC = new bool[size];
            _error = Cl.EnqueueReadBuffer<bool>(cmdQueue, outputBuffer, Bool.True, 0, size, arrC, 0, null, out _event);
            CheckErr(_error, "EnqueueReadBuffer");
            */

            _error = Cl.ReleaseKernel(kernel);
            CheckErr(_error, "ReleaseKernel");

            _error = Cl.ReleaseCommandQueue(cmdQueue);
            CheckErr(_error, "ReleaseCommandQueue");

            for (int i = 0; i < triangleMemo.Length; i++)
            {
                _error = Cl.ReleaseMemObject(triangleMemo[i]);
                CheckErr(_error, "ReleaseMemObject");
            }

            for (int i = 0; i < rayMemo.Length; i++)
            {
                _error = Cl.ReleaseMemObject(rayMemo[i]);
                CheckErr(_error, "ReleaseMemObject");
            }

            _error = Cl.ReleaseMemObject(outputBuffer);
            CheckErr(_error, "ReleaseMemObject");

            //GCHandle pinnedOutputArray = GCHandle.Alloc(arrC, GCHandleType.Pinned);
            //pinnedOutputArray.Free();

            return result.ToArray();
        }

        [Obsolete]
        public bool[] Run(int size, System.ArraySegment<float>[] args, string cFuncName)
        {
            ErrorCode _error;
            Event _event;

            Kernel kernel = Cl.CreateKernel(Program, cFuncName, out _error);
            CheckErr(_error, "CreateBuffer");

            IMem<float>[] memo = new IMem<float>[args.Count()];
            for(int i = 0; i < memo.Count(); i++)
            {
                memo[i] = Cl.CreateBuffer<float>(Context, MemFlags.AllocHostPtr | MemFlags.ReadOnly, args[i].Count, out _error);
                CheckErr(_error, "CreateBuffer");
            }

            var outputBuffer = Cl.CreateBuffer<bool>(Context, MemFlags.AllocHostPtr | MemFlags.WriteOnly, size, out _error);
            CheckErr(_error, "CreateBuffer");

            var cmdQueue = Cl.CreateCommandQueue(Context, Device, CommandQueueProperties.None, out _error);
            CheckErr(_error, "CreateCommandQueue");

            for (int i = 0; i < memo.Count(); i++)
            {
                _error = Cl.EnqueueWriteBuffer<float>(cmdQueue, memo[i], Bool.True, 0, args[i].Count, args[i].ToArray(), 0, null, out _event);
                CheckErr(_error, "EnqueueWriteBuffer");
            }

            for (int i = 0; i < memo.Count(); i++)
            {
                _error = Cl.SetKernelArg<float>(kernel, (uint)i, memo.ElementAt(i));
                CheckErr(_error, "SetKernelArg");
            }

            _error = Cl.SetKernelArg<bool>(kernel, (uint)args.Length, outputBuffer);
            CheckErr(_error, "SetKernelArg");

            _error = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, new IntPtr[] { (IntPtr)size, (IntPtr)1, (IntPtr)1 }, null, 0, null, out _event);
            CheckErr(_error, "EnqueueNDRangeKernel");

            _error = Cl.Finish(cmdQueue);
            CheckErr(_error, "Finish");

            bool[] arrC = new bool[size];
            _error = Cl.EnqueueReadBuffer<bool>(cmdQueue, outputBuffer, Bool.True, 0, size, arrC, 0, null, out _event);
            CheckErr(_error, "EnqueueReadBuffer");

            _error = Cl.ReleaseKernel(kernel);
            CheckErr(_error, "ReleaseKernel");

            _error = Cl.ReleaseCommandQueue(cmdQueue);
            CheckErr(_error, "ReleaseCommandQueue");

            for (int i = 0; i < memo.Count(); i++)
            {
                _error = Cl.ReleaseMemObject(memo[i]);
                CheckErr(_error, "ReleaseMemObject");
            }

            _error = Cl.ReleaseMemObject(outputBuffer);
            CheckErr(_error, "ReleaseMemObject");

            GCHandle pinnedOutputArray = GCHandle.Alloc(arrC, GCHandleType.Pinned);
            pinnedOutputArray.Free();

            return arrC.ToArray();
        }

        private void CheckErr(ErrorCode err, string name)
        {
            var prev = Console.ForegroundColor;
            if (err != ErrorCode.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {name} ({err})");
            }
            Console.ForegroundColor = prev;
        }
    }
}