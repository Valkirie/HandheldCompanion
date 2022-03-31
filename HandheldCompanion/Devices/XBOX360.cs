using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Devices
{
    internal class XBOX360 : HandheldDevice
    {
        // Specific groups (move me)
        Model3DGroup ALetter;
        Model3DGroup BLetter;
        Model3DGroup XLetter;
        Model3DGroup YLetter;
        Model3DGroup MainBodyCharger;
        Model3DGroup XBoxButton;
        Model3DGroup XboxButtonRing;

        public XBOX360(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName, "XBOX360")
        {
            // colors
            ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#333333");
            ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#F0EFF0");
            ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
            
            MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-42.231f, -6.10f, 21.436f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(21.013f, -6.1f, -3.559f);
            JoystickMaxAngleDeg = 19.0f;
            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-44.668f, 3.087f, 39.705);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(44.668f, 3.087f, 39.705);
            TriggerMaxAngleDeg = 16.0f;

            // load model(s)
            ALetter = modelImporter.Load($"models/{ModelName}/A-Letter.obj");
            BLetter = modelImporter.Load($"models/{ModelName}/B-Letter.obj");
            XLetter = modelImporter.Load($"models/{ModelName}/X-Letter.obj");
            YLetter = modelImporter.Load($"models/{ModelName}/Y-Letter.obj");
            MainBodyCharger = modelImporter.Load($"models/{ModelName}/MainBody-Charger.obj");
            XBoxButton = modelImporter.Load($"models/{ModelName}/XBoxButton.obj");
            XboxButtonRing = modelImporter.Load($"models/{ModelName}/XboxButtonRing.obj");

            // pull model(s)
            model3DGroup.Children.Add(ALetter);
            model3DGroup.Children.Add(BLetter);
            model3DGroup.Children.Add(XLetter);
            model3DGroup.Children.Add(YLetter);
            model3DGroup.Children.Add(MainBodyCharger);
            model3DGroup.Children.Add(XBoxButton);
            model3DGroup.Children.Add(XboxButtonRing);

            foreach (Model3DGroup model3D in model3DGroup.Children)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;

            // specific color(s)
            ((GeometryModel3D)MainBody.Children[0]).Material = MaterialPlasticWhite;
        }
    }
}
