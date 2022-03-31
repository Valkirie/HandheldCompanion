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
    internal class AYANEO2021 : HandheldDevice
    {
        // Specific groups (move me)
        Model3DGroup WFBEsc;
        Model3DGroup WFBH;
        Model3DGroup WFBKB;
        Model3DGroup WFBRGB;
        Model3DGroup WFBTM;
        Model3DGroup WFBWin;
        Model3DGroup ShoulderLeftMiddle;
        Model3DGroup ShoulderRightMiddle;

        public AYANEO2021(string ManufacturerName, string ProductName) : base(ManufacturerName, ProductName, "AYANEO 2021")
        {
            // colors
            ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#333333");
            ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#F0EFF0");
            ColorHighlight = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"];
            
            MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));
            MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-109.0f, -8.0f, 23.0f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(104.0f, -8.0f, -6.0f);
            JoystickMaxAngleDeg = 19.0f;
            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-105.951f, 1.25f, 46.814f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(105.951f, 1.25f, 46.814f);

            // load model(s)
            WFBEsc = modelImporter.Load($"models/{ModelName}/WFB-Esc.obj");
            WFBH = modelImporter.Load($"models/{ModelName}/WFB-H.obj");
            WFBKB = modelImporter.Load($"models/{ModelName}/WFB-KB.obj");
            WFBRGB = modelImporter.Load($"models/{ModelName}/WFB-RGB.obj");
            WFBTM = modelImporter.Load($"models/{ModelName}/WFB-TM.obj");
            WFBWin = modelImporter.Load($"models/{ModelName}/WFB-Win.obj");
            ShoulderLeftMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Left-Middle.obj");
            ShoulderRightMiddle = modelImporter.Load($"models/{ModelName}/Shoulder-Right-Middle.obj");

            // pull model(s)
            model3DGroup.Children.Add(WFBEsc);
            model3DGroup.Children.Add(WFBH);
            model3DGroup.Children.Add(WFBKB);
            model3DGroup.Children.Add(WFBRGB);
            model3DGroup.Children.Add(WFBTM);
            model3DGroup.Children.Add(WFBWin);
            model3DGroup.Children.Add(ShoulderLeftMiddle);
            model3DGroup.Children.Add(ShoulderRightMiddle);

            foreach (Model3DGroup model3D in model3DGroup.Children)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;

            // specific color(s)
            ((GeometryModel3D)MainBody.Children[0]).Material = MaterialPlasticWhite;
        }
    }
}
