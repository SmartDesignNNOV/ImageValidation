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

namespace ImageValidation.Sample
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private Dialog _progressDialog;

        private ImageView _imageView;

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

            _detector = Factory.CreateReceiptDetector();
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
            _ = PickAndProcessPhotoAsync();
        }

        private async Task PickAndProcessPhotoAsync()
        {
            try
            {
                var photo = await MediaPicker.PickPhotoAsync();

                _progressDialog = CreateDialogProgressBar();

                _progressDialog.Show();

                using Stream input = await photo.OpenReadAsync();

                Bitmap image = await BitmapFactory.DecodeStreamAsync(input);
                _imageView.SetImageBitmap(image);

                input.Seek(0, SeekOrigin.Begin);
                var res = await _detector.GetReceiptProbability(input);

                _progressDialog.Hide();
                _progressDialog = null;

                AlertDialog.Builder alert = new AlertDialog.Builder(this);
                alert.SetMessage($"Is Receipt Probability: {res}");

                alert.SetPositiveButton("OK", (s, a) => { });
                Dialog dialog = alert.Create();
                dialog.Show();
            }
            catch (FeatureNotSupportedException e)
            {
                _progressDialog.Hide();
                _progressDialog = null;
                // Feature is not supported on the device
            }
            catch (PermissionException e)
            {
                _progressDialog.Hide();
                _progressDialog = null;
                // Permissions not granted
            }
            catch (Exception e)
            {
                _progressDialog.Hide();
                _progressDialog = null;
                Console.WriteLine($"PickAndProcessPhotoAsync Exception: {e}");
            }
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
