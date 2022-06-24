using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;
using System.Threading.Tasks;
using Xamarin.Essentials;
using System.IO;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using Android.Widget;
using Toolbar = AndroidX.AppCompat.Widget.Toolbar;
using System.Linq;
using Android.Graphics;
using AndroidX.ExifInterface.Media;
using Android.Content.PM;

namespace ImageValidation.Sample
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity
    {
        private Dialog _progressDialog;

        private ImageView _imageView;
        private TextView _textView1;
        private TextView _textView2;

        private ReceiptDetector _detector;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            ImageValidation.Platform.Init();

            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            _imageView = FindViewById<ImageView>(Resource.Id.imageView1);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            FloatingActionButton fab1 = FindViewById<FloatingActionButton>(Resource.Id.fab1);
            fab1.Click += Fab1OnClick;

            _textView1 = FindViewById<TextView>(Resource.Id.textView1);
            _textView2 = FindViewById<TextView>(Resource.Id.textView2);

            _detector = Factory.CreateReceiptDetector();

            _textView1.Visibility = ViewStates.Gone;
            _textView2.Visibility = ViewStates.Gone;
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            _ = CaptureAndProcessPhotoAsync();
        }

        private void Fab1OnClick(object sender, EventArgs eventArgs)
        {
            _ = PickAndProcessPhotoAsync();
        }

        private async Task PickAndProcessPhotoAsync()
        {
            if (await Permissions.CheckStatusAsync<Permissions.Camera>() != PermissionStatus.Granted
                || await Permissions.CheckStatusAsync<Permissions.StorageRead>() != PermissionStatus.Granted
                || await Permissions.CheckStatusAsync<Permissions.StorageWrite>() != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.Camera>();
                await Permissions.RequestAsync<Permissions.StorageRead>();
                await Permissions.RequestAsync<Permissions.StorageWrite>();
                return;
            }

            try
            {
                var photo = await MediaPicker.PickPhotoAsync();

                if (photo == null) return;

                Stream originalInput = await photo.OpenReadAsync();
                await ProcessPhotoAsync(originalInput);
            }
            catch (FeatureNotSupportedException e)
            {
            }
            catch (PermissionException e)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine($"PickAndProcessPhotoAsync Exception: {e}");
            }
        }

        private async Task CaptureAndProcessPhotoAsync()
        {
            if (await Permissions.CheckStatusAsync<Permissions.Camera>() != PermissionStatus.Granted
                || await Permissions.CheckStatusAsync<Permissions.StorageRead>() != PermissionStatus.Granted
                || await Permissions.CheckStatusAsync<Permissions.StorageWrite>() != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.Camera>();
                await Permissions.RequestAsync<Permissions.StorageRead>();
                await Permissions.RequestAsync<Permissions.StorageWrite>();
                return;
            }

            try
            {
                var photo = await MediaPicker.CapturePhotoAsync();

                if (photo == null) return;

                Stream originalInput = await photo.OpenReadAsync();
                await ProcessPhotoAsync(originalInput);
            }
            catch (FeatureNotSupportedException e)
            {
            }
            catch (PermissionException e)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine($"PickAndProcessPhotoAsync Exception: {e}");
            }
        }

        private async Task ProcessPhotoAsync(Stream originalInput)
        {
            try
            {
                _imageView.SetImageBitmap(null);
                _textView1.Visibility = ViewStates.Gone;
                _textView2.Visibility = ViewStates.Gone;

                _progressDialog = CreateDialogProgressBar();
                _progressDialog.Show();

                // Compensate Exif rotation if needed
                using Stream input = await ConvertToJPGStream(originalInput, 60);

                originalInput.Dispose();

                input.Seek(0, SeekOrigin.Begin);
                Bitmap image = await BitmapFactory.DecodeStreamAsync(input);
                _imageView.SetImageBitmap(image);

                input.Seek(0, SeekOrigin.Begin);
                var res = await _detector.GetReceiptProbability(input);

                _progressDialog.Hide();
                _progressDialog = null;

                if (res > 0.4f)
                {
                    _textView1.Visibility = ViewStates.Visible;
                    _textView2.Visibility = ViewStates.Gone;
                }
                else
                {
                    _textView1.Visibility = ViewStates.Gone;
                    _textView2.Visibility = ViewStates.Visible;
                }

                //AlertDialog.Builder alert = new AlertDialog.Builder(this);
                //alert.SetMessage($"Is Receipt Probability: {res}");

                //alert.SetPositiveButton("OK", (s, a) => { });
                //Dialog dialog = alert.Create();
                //dialog.Show();
            }
            catch (Exception e)
            {
                _progressDialog.Hide();
                _progressDialog = null;
                Console.WriteLine($"PickAndProcessPhotoAsync Exception: {e}");
            }
        }

        private const int OrientationNormal = 0;
        private const int OrientationRotate90 = 90;
        private const int OrientationRotate180 = 180;
        private const int OrientationRotate270 = -90;

        private static int GetRotationDegree(Stream stream)
        {
            var exif = new ExifInterface(stream);
            int orientation = exif.GetAttributeInt(ExifInterface.TagOrientation, 0);
            switch (orientation)
            {
                case 3:
                case 4:
                    return OrientationRotate180;
                case 5:
                case 6:
                    return OrientationRotate90;
                case 7:
                case 8:
                    return OrientationRotate270;
                default:
                    return OrientationNormal;
            }
        }

        private static Bitmap RotateBitmap(Bitmap image, float degree)
        {
            Bitmap rotatedImage = null;
            if (degree != OrientationNormal)
            {
                var matrix = new Matrix();
                matrix.PostRotate(degree);
                rotatedImage = Bitmap.CreateBitmap(image, 0, 0, image.Width, image.Height, matrix, false);
            }

            return rotatedImage ?? image;
        }

        internal static Task<Stream> ConvertToJPGStream(Stream stream, int jpegCompressionQuality)
        {
            return Task.Run<System.IO.Stream>(() =>
            {
                int degree = GetRotationDegree(stream);
                stream.Seek(0, SeekOrigin.Begin);

                var options = new BitmapFactory.Options();
                Bitmap source = BitmapFactory.DecodeStream(stream, null, options);
                Bitmap result = RotateBitmap(source, degree);

                if (!ReferenceEquals(source, result))
                {
                    source.Recycle();
                    source.Dispose();
                }
                var outStream = new MemoryStream();
                result.Compress(Bitmap.CompressFormat.Jpeg, jpegCompressionQuality, outStream);

                outStream.Seek(0, SeekOrigin.Begin);

                result.Recycle();
                result.Dispose();

                return outStream;
            });
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private Dialog CreateDialogProgressBar()
        {
            AlertDialog.Builder alertBuilder = new AlertDialog.Builder(this);
            alertBuilder.SetTitle("Processing...");
            alertBuilder.SetCancelable(false);

            ProgressBar progressBar = new ProgressBar(this);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WrapContent,
                LinearLayout.LayoutParams.WrapContent);
            progressBar.LayoutParameters = lp;
            alertBuilder.SetView(progressBar);

            return alertBuilder.Create();
        }
    }
}
