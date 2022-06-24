using Android.App;
using Android.Content;
using Android.Runtime;
using System;
using System.Runtime;
using Android.OS;
using Environment = System.Environment;

namespace NIQ.CPS.Hermes.Droid
{
    [Application(LargeHeap = true)]
    public class DroidApp : Application
    {
        protected DroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();
        }

        public override void OnLowMemory()
        {
            base.OnLowMemory();
        }

        public override void OnTrimMemory([GeneratedEnum] TrimMemory level)
        {
            base.OnTrimMemory(level);
        }
    }
}