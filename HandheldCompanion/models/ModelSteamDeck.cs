using ControllerCommon.Controllers;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace HandheldCompanion.Models
{
    internal class ModelSteamDeck : Model
    {
        // Specific groups
        Model3DGroup BackIcon;
        Model3DGroup VolumeUp;
        Model3DGroup VolumeDown;
        Model3DGroup ThreeDots;
        Model3DGroup MainBodyLeftOver;
        Model3DGroup SteamText;
        Model3DGroup StartIcon;
        Model3DGroup Screen;
        Model3DGroup PowerButton;

        Model3DGroup JoystickLeftTouch;
        Model3DGroup JoystickRightTouch;
        Model3DGroup LeftPadTouch;
        Model3DGroup RightPadTouch;

        Model3DGroup B1Symbol;
        Model3DGroup B2Symbol;
        Model3DGroup B3Symbol;
        Model3DGroup B4Symbol;

        public ModelSteamDeck() : base("SteamDeck")
        {
            // colors
            var ColorPlasticBlack = (Color)ColorConverter.ConvertFromString("#4F4D50");
            var ColorPlasticDarkGrey = (Color)ColorConverter.ConvertFromString("#605F5D");
            var ColorPlasticWhite = (Color)ColorConverter.ConvertFromString("#A7A5A6");

            var MaterialPlasticBlack = new DiffuseMaterial(new SolidColorBrush(ColorPlasticBlack));
            var MaterialPlasticDarkGrey = new DiffuseMaterial(new SolidColorBrush(ColorPlasticDarkGrey));
            var MaterialPlasticWhite = new DiffuseMaterial(new SolidColorBrush(ColorPlasticWhite));

            // Rotation Points
            JoystickRotationPointCenterLeftMillimeter = new Vector3D(-102.83f, 6.15f, 34.5f);
            JoystickRotationPointCenterRightMillimeter = new Vector3D(102.83f, 6.15f, 34.5f);
            JoystickMaxAngleDeg = 14.0f;

            ShoulderTriggerRotationPointCenterLeftMillimeter = new Vector3D(-145.6f, 17.475f, 41.0f);
            ShoulderTriggerRotationPointCenterRightMillimeter = new Vector3D(145.6f, 17.475f, 41.0f);
            TriggerMaxAngleDeg = 16.0f;

            UpwardVisibilityRotationAxisLeft = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationAxisRight = new Vector3D(1, 0, 0);
            UpwardVisibilityRotationPointLeft = new Vector3D(-102.528f, 0.0f, 58.365f);
            UpwardVisibilityRotationPointRight = new Vector3D(102.528f, 0.0f, 58.365f);

            // load model(s)
            BackIcon = modelImporter.Load($"models/{ModelName}/BackIcon.obj");
            VolumeUp = modelImporter.Load($"models/{ModelName}/VolumeUp.obj");
            VolumeDown = modelImporter.Load($"models/{ModelName}/VolumeDown.obj");
            ThreeDots = modelImporter.Load($"models/{ModelName}/ThreeDots.obj");
            MainBodyLeftOver = modelImporter.Load($"models/{ModelName}/MainBodyLeftOver.obj");
            SteamText = modelImporter.Load($"models/{ModelName}/SteamText.obj");
            StartIcon = modelImporter.Load($"models/{ModelName}/StartIcon.obj");
            Screen = modelImporter.Load($"models/{ModelName}/Screen.obj");
            PowerButton = modelImporter.Load($"models/{ModelName}/PowerButton.obj");

            JoystickLeftTouch = modelImporter.Load($"models/{ModelName}/JoystickLeftTouch.obj");
            JoystickRightTouch = modelImporter.Load($"models/{ModelName}/JoystickRightTouch.obj");
            LeftPadTouch = modelImporter.Load($"models/{ModelName}/LeftPadTouch.obj");
            RightPadTouch = modelImporter.Load($"models/{ModelName}/RightPadTouch.obj");

            B1Symbol = modelImporter.Load($"models/{ModelName}/B1-Symbol.obj");
            B2Symbol = modelImporter.Load($"models/{ModelName}/B2-Symbol.obj");
            B3Symbol = modelImporter.Load($"models/{ModelName}/B3-Symbol.obj");
            B4Symbol = modelImporter.Load($"models/{ModelName}/B4-Symbol.obj");

            // pull model(s)
            model3DGroup.Children.Add(BackIcon);
            model3DGroup.Children.Add(VolumeUp);
            model3DGroup.Children.Add(VolumeDown);
            model3DGroup.Children.Add(ThreeDots);
            model3DGroup.Children.Add(MainBodyLeftOver);
            model3DGroup.Children.Add(SteamText);
            model3DGroup.Children.Add(StartIcon);
            model3DGroup.Children.Add(Screen);
            model3DGroup.Children.Add(PowerButton);

            model3DGroup.Children.Add(JoystickLeftTouch);
            model3DGroup.Children.Add(JoystickRightTouch);
            model3DGroup.Children.Add(LeftPadTouch);
            model3DGroup.Children.Add(RightPadTouch);

            model3DGroup.Children.Add(B1Symbol);
            model3DGroup.Children.Add(B2Symbol);
            model3DGroup.Children.Add(B3Symbol);
            model3DGroup.Children.Add(B4Symbol);

            // specific button material(s)
            foreach (ControllerButtonFlags button in Enum.GetValues(typeof(ControllerButtonFlags)))
            {
                Material buttonMaterial = null;

                if (ButtonMap.ContainsKey(button))
                    foreach (var model3D in ButtonMap[button])
                    {
                        switch (button)
                        {
                            case ControllerButtonFlags.B1:
                            case ControllerButtonFlags.B2:
                            case ControllerButtonFlags.B3:
                            case ControllerButtonFlags.B4:
                            case ControllerButtonFlags.DPadUp:
                            case ControllerButtonFlags.DPadRight:
                            case ControllerButtonFlags.DPadDown:
                            case ControllerButtonFlags.DPadLeft:
                            case ControllerButtonFlags.LeftThumb:
                            case ControllerButtonFlags.RightThumb:
                                buttonMaterial = MaterialPlasticBlack;
                                break;
                            default:
                                buttonMaterial = MaterialPlasticDarkGrey;
                                break;
                        }

                        DefaultMaterials[model3D] = buttonMaterial;
                        ((GeometryModel3D)model3D.Children[0]).Material = buttonMaterial;
                        ((GeometryModel3D)model3D.Children[0]).BackMaterial = buttonMaterial;

                    }
            }

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                if (DefaultMaterials.ContainsKey(model3D))
                    continue;

                // default material(s)
                ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticDarkGrey;
                ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticDarkGrey;
                DefaultMaterials[model3D] = MaterialPlasticDarkGrey;

                // specific material(s)
                if (model3D == Screen || model3D == LeftThumbRing || model3D == RightThumbRing)
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticBlack;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticBlack;
                    DefaultMaterials[model3D] = MaterialPlasticBlack;
                    continue;
                }

                // specific material(s)
                if (model3D == SteamText || model3D == ThreeDots
                    || model3D == BackIcon || model3D == StartIcon
                    || model3D == B1Symbol || model3D == B2Symbol || model3D == B3Symbol || model3D == B4Symbol
                    || model3D == JoystickLeftTouch || model3D == JoystickRightTouch
                    )
                {
                    ((GeometryModel3D)model3D.Children[0]).Material = MaterialPlasticWhite;
                    ((GeometryModel3D)model3D.Children[0]).BackMaterial = MaterialPlasticWhite;
                    DefaultMaterials[model3D] = MaterialPlasticWhite;
                    continue;
                }
            }

            DrawHighligths();
        }

        private new void DrawHighligths()
        {
            var ColorHighlight = (Brush)Application.Current.Resources["AccentButtonBackground"];
            var MaterialHighlight = new DiffuseMaterial(ColorHighlight);

            foreach (Model3DGroup model3D in model3DGroup.Children)
            {
                // generic material(s)
                HighlightMaterials[model3D] = MaterialHighlight;
            }
        }
    }
}
