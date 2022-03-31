using OpenCL.Net;
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
        private string CFuncName;

        private void ContextNotify(string errInfo, byte[] data, IntPtr cb, IntPtr userData)
        {
            Console.WriteLine("OpenCL Notification: " + errInfo);
        }

        public OpenCLProgram(string cFunc, string cFuncName)
        {
            CFuncName= cFuncName;
            ErrorCode error;
            Platform = Cl.GetPlatformIDs(out error)[0];
            CheckErr(error, "GetPlatformIDs");
            Device = Cl.GetDeviceIDs(Platform, DeviceType.All, out error)[0];
            CheckErr(error, "GetDeviceIDs");
            Context = Cl.CreateContext(null, 1, new[] { Device }, ContextNotify, IntPtr.Zero, out error);
            CheckErr(error, "CreateContext");
            Program = Cl.CreateProgramWithSource(Context, 1, new[] { cFunc }, null, out error);
            CheckErr(error, "CreateProgramWithSource");
            error = Cl.BuildProgram(Program, 1, new[] { Device }, string.Empty, null, IntPtr.Zero);
            CheckErr(error, "BuildProgram");
        }

        public bool[] Run(float[][] args)
        {
            ErrorCode error;
            int size = args[0].Length;
            Kernel kernel = Cl.CreateKernel(Program, CFuncName, out error);
            MemFlags flags = MemFlags.UseHostPtr | MemFlags.ReadWrite;

            IMem<float>[] memo = new IMem<float>[args.Count()];
            for(int i = 0; i < memo.Count(); i++)
            {
                Cl.CreateBuffer<float>(Context, flags, args[i], out error);
                CheckErr(error, "CreateBuffer");
            }
            var outputBuffer = Cl.CreateBuffer<bool>(Context, MemFlags.AllocHostPtr | MemFlags.ReadWrite, size, out error);
            CheckErr(error, "CreateBuffer");
            for (int i = 0; i < memo.Count(); i++) Cl.SetKernelArg<float>(kernel, (uint)i, memo.ElementAt(i));
            Cl.SetKernelArg<bool>(kernel, (uint)args.Length, outputBuffer);
            var cmdQueue = Cl.CreateCommandQueue(Context, Device, CommandQueueProperties.None, out error);
            CheckErr(error, "CreateCommandQueue");
            if(cmdQueue.IsValid())
            {
                IntPtr[] workGroupSizePtr = new IntPtr[] { (IntPtr)size, (IntPtr)1, (IntPtr)1 };
                error = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, workGroupSizePtr, null, 0, null, out _);
                CheckErr(error, "EnqueueNDRangeKernel");
                error = Cl.Finish(cmdQueue);
                CheckErr(error, "Finish");
                bool[] arrC = new bool[size];
                error = Cl.EnqueueReadBuffer<bool>(cmdQueue, outputBuffer, Bool.True, 0, size, arrC, 0, null, out _);
                CheckErr(error, "EnqueueReadBuffer");
                error = Cl.ReleaseKernel(kernel);
                CheckErr(error, "ReleaseKernel");
                error = Cl.ReleaseCommandQueue(cmdQueue);
                CheckErr(error, "ReleaseCommandQueue");

                for (int i = 0; i < memo.Count(); i++)
                {
                    error = Cl.ReleaseMemObject(memo[i]);
                    CheckErr(error, "ReleaseMemObject");
                }
                error = Cl.ReleaseMemObject(outputBuffer);
                CheckErr(error, "ReleaseMemObject");

                GCHandle pinnedOutputArray = GCHandle.Alloc(arrC, GCHandleType.Pinned);
                pinnedOutputArray.Free();
                return arrC.ToArray();
            }
            Console.WriteLine(":(");
            return null;
        }

        /*
        public bool[] RunCircles2(float[][] args)
        {
            ErrorCode error;
            int size = args[0].Length;
            Kernel kernel = Cl.CreateKernel(Program, CFuncName, out error);
            CheckErr(error, "CreateKernel");

            MemFlags flags = MemFlags.UseHostPtr | MemFlags.ReadWrite;

            var fbuffer0 = Cl.CreateBuffer<float>(Context, flags, args[0], out error);
            CheckErr(error, "fbuffer0");
            var fbuffer1 = Cl.CreateBuffer<float>(Context, flags, args[1], out error);
            CheckErr(error, "fbuffer1");
            var fbuffer2 = Cl.CreateBuffer<float>(Context, flags, args[2], out error);
            CheckErr(error, "fbuffer2");
            var fbuffer3 = Cl.CreateBuffer<float>(Context, flags, args[3], out error);
            CheckErr(error, "fbuffer3");
            var fbuffer4 = Cl.CreateBuffer<float>(Context, flags, args[4], out error);
            CheckErr(error, "fbuffer4");
            var fbuffer5 = Cl.CreateBuffer<float>(Context, flags, args[5], out error);
            CheckErr(error, "fbuffer5");
            var fbuffer6 = Cl.CreateBuffer<float>(Context, flags, args[6], out error);
            CheckErr(error, "fbuffer6");
            var bbuffer7 = Cl.CreateBuffer<bool>(Context, MemFlags.AllocHostPtr | MemFlags.ReadWrite, size, out error);
            CheckErr(error, "bbuffer7");

            error = Cl.SetKernelArg<float>(kernel, 0, fbuffer0);
            CheckErr(error, "SetKernelArg");
            error = Cl.SetKernelArg<float>(kernel, 1, fbuffer1);
            CheckErr(error, "SetKernelArg");
            error = Cl.SetKernelArg<float>(kernel, 2, fbuffer2);
            CheckErr(error, "SetKernelArg");
            error = Cl.SetKernelArg<float>(kernel, 3, fbuffer3);
            CheckErr(error, "SetKernelArg");
            error = Cl.SetKernelArg<float>(kernel, 4, fbuffer4);
            CheckErr(error, "SetKernelArg");
            error = Cl.SetKernelArg<float>(kernel, 5, fbuffer5);
            CheckErr(error, "SetKernelArg");
            error =  Cl.SetKernelArg<float>(kernel, 6, fbuffer6);
            CheckErr(error, "SetKernelArg");
            error = Cl.SetKernelArg<bool>(kernel, 7, bbuffer7);
            CheckErr(error, "SetKernelArg");

            var cmdQueue = Cl.CreateCommandQueue(Context, Device, CommandQueueProperties.None, out error);
            CheckErr(error, "CreateCommandQueue");
            Event clevent;
            IntPtr[] workGroupSizePtr = new IntPtr[] { (IntPtr)size };
            error = Cl.EnqueueNDRangeKernel(cmdQueue, kernel, 1, null, workGroupSizePtr, null, 0, null, out clevent);
            CheckErr(error, "EnqueueNDRangeKernel");
            error = Cl.Finish(cmdQueue);
            CheckErr(error, "Finish");
            bool[] arrC = new bool[size];
            error = Cl.EnqueueReadBuffer<bool>(cmdQueue, bbuffer7, Bool.True, 0, size, arrC, 0, null, out clevent);

            Cl.ReleaseKernel(kernel);
            Cl.ReleaseCommandQueue(cmdQueue);

            Cl.ReleaseMemObject(fbuffer0);
            Cl.ReleaseMemObject(fbuffer1);
            Cl.ReleaseMemObject(fbuffer2);
            Cl.ReleaseMemObject(fbuffer3);
            Cl.ReleaseMemObject(fbuffer4);
            Cl.ReleaseMemObject(fbuffer5);
            Cl.ReleaseMemObject(fbuffer6);
            Cl.ReleaseMemObject(bbuffer7);

            //Get a pointer to our unmanaged output byte[] array
            GCHandle pinnedOutputArray = GCHandle.Alloc(arrC, GCHandleType.Pinned);
            pinnedOutputArray.Free();
            return arrC;
        }
        */
        private void CheckErr(ErrorCode err, string name)
        {
            if (err != ErrorCode.Success)
            {
                Console.WriteLine($"ERROR: {name} ({err})");
                throw new Exception($"ERROR: {name} ({err})");
            }
        }
    }
}