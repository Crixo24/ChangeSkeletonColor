//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------


/*IMPORTANTE: Este es un proyecto que hemos encontrado en githug y que hemos aprovechado para nuestra práctica. Dicho
 * proyecto se puede encontrar en --> https://github.com/boatski/KinectSkeleton
    De hecho, se puede apreciar de quién es mirando el copyright que hay encima de este comentario. Lo único que
    nosotros hemos modificado ha sido:
 *      - Limitar el número de trackings de 10 que estaba a 1 sólo, que es lo que nos intersaba.
 *      - Hemos creado la función CambiaColor (línea 475), que recibe como parámetro la lista de skeletons (que ahora sólo
 *      contiene un skeleton). Esta función simplemente obtiene cada vez que se llama, la coordenada Y de la mano
 *      derecha, el centro de la cadera, el centro de los hombros y la cabeza. Según dónde se encuentre la mano derecha 
 *      en base a estos puntos, cambiará el valor de la variable "trackedBonePen" que es la que almacena
 *      el color con el que se dibujará el skeleton. Obviamente, hay que llamar a esta función antes que a la 
 *      de dibujar.
*/

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System.Windows.Shapes;
    using System.Windows.Controls;
    using System.Collections.Generic;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Declaración de las variables de la clase

        private List<Skeleton> skeletonList = new List<Skeleton>();
        private int listSize = 1;
        private int frameInterval = 3;
        private int frameCount = 0;

        private const double HeadSize = 0.075;
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;///0.5f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;///0.5f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;
        private const double HeadThickness = 25;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 10, 255, 255));

        private readonly Brush headBrush = new SolidColorBrush(Color.FromArgb(255, 255, 25, 25));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private Pen trackedBonePen = new Pen(Brushes.Blue, 6);
        private Pen graphPen = new Pen(Brushes.White, 2);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Translates the skeletons position based on its location in the List
        /// </summary>
        /// <param name="joint">Joint of the skeleton being translated</param>
        /// <param name="pos">Position in the list</param>
        /// <returns>Returns a new point</returns>
        private SkeletonPoint TranslateSkeletonPosition(Joint joint, int pos)
        {
            float trans = 0.05f;

            float skeleX = joint.Position.X;
            float skeleY = joint.Position.Y;
            float skeleZ = joint.Position.Z;

            skeleX += trans*pos;
            skeleY += trans*pos;
            skeleZ += trans*pos;

            SkeletonPoint skelePoint = new SkeletonPoint()
            {
                X = (float)skeleX, 
                Y = (float)skeleY, 
                Z = (float)skeleZ
            };

            return skelePoint;
            //pos++;
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        // Restrict the list of skeletons to 10
                        if (skeletonList.Count > listSize)
                        {
                            skeletonList.RemoveAt(listSize - 1);
                        }// end if

                        // Only capture the skeleton every 2 frames
                        if (frameCount % frameInterval == 0)
                        {
                            skeletonList.Insert(0, skel);
                            frameCount = 0;
                        }// end if
                        frameCount++;

                        

                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                           
                            this.DrawBonesAndJoints(skeletonList, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(List<Skeleton> skel, DrawingContext drawingContext)
        {
            //drawingContext.DrawLine(graphPen, new Point(340.0, 420.0), new Point(560.0, 200.0));
            
            for (int i = listSize - 1; i >= 0; i--)
            {
                Skeleton skeleton = skel[i];
                // Render Torso
                this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter, i);
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine, i);
                this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter, i);
                this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight, i);

                // Left Arm
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft, i);

                // Right Arm
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight, i);

                // Left Leg
                this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft, i);
                this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft, i);

                // Right Leg
                this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight, i);
                this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight, i);
                
                // Render Joints
                foreach (Joint joint in skeleton.Joints)
                {
                    Brush drawBrush = null;

                    if (joint.TrackingState == JointTrackingState.Tracked)
                    {
                        drawBrush = this.trackedJointBrush;
                    }
                    else if (joint.TrackingState == JointTrackingState.Inferred)
                    {
                        drawBrush = this.inferredJointBrush;
                    }

                    if (drawBrush != null)
                    {
                        // If joint type is Head, then draw a big circle
                        if (joint.JointType == JointType.Head)
                        {
                            //drawingContext.DrawEllipse(headBrush, null, this.SkeletonPointToScreen(joint.Position), HeadThickness, HeadThickness);
                            drawingContext.DrawEllipse(headBrush, null, this.SkeletonPointToScreen(TranslateSkeletonPosition(joint, i)), HeadThickness, HeadThickness);
                        }
                        else
                        {
                            //drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                            drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(TranslateSkeletonPosition(joint, i)), JointThickness, JointThickness);
                        }

                        if (joint.JointType == JointType.HipCenter)
                        {
                            //Console.Write("X: " + joint.Position.X + "\nY: " + joint.Position.Y + "\n");
                        }// end if
                    }// end if
                }// end foreach

                
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1, int pos)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                CambiaColor(skeletonList);
                drawPen = this.trackedBonePen;
            }

            //drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(this.TranslateSkeletonPosition(joint0, pos)), this.SkeletonPointToScreen(this.TranslateSkeletonPosition(joint1, pos)));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }

        /*Esta función es la que cambia el color del esqueleto en base a la posición de la mano derecha*/
        private void CambiaColor(List<Skeleton> skel)
        {
            //skel sólo tiene un esqueleto porque lo hemos limitado a ese número en las variables del principio
            Skeleton s = skel[0];
            //Sacamos los datos (puntos de las articulaciones) del skeleton cuyo tracking está haciendo
            Joint rightHand = s.Joints[JointType.HandRight];        //Obtenemos la mano derecha
            Joint head = s.Joints[JointType.Head];                  //Obtenemos la cabeza
            Joint hips = s.Joints[JointType.HipCenter];             //Obtenemos el punto central de la cadera
            Joint shoulder = s.Joints[JointType.ShoulderCenter];    //Obtenemos el punto central de los hombros

            //Aquí, obtenemos la coordenada Y de cada uno de los puntos (articulaciones) obtenidas.
            double rightHandY = rightHand.Position.Y;
            double head_Y = head.Position.Y;
            double hips_Y = hips.Position.Y;
            double shoulder_Y = shoulder.Position.Y;

            /* Tan simple como comparar la coordenada Y de los putos que nos interesan (centro cadera, centro hombros 
             * y cabeza) con la de la mano derecha y, según dónde se encuentre la mano derecha, así se cambia
             * el valor de la variable que indica de qué color se dibujará el skeleton
             */

            if (rightHandY < hips_Y) //Si la mano derecha está por debajo de la cadera, se dibuja AZUL
            {
                this.trackedBonePen = new Pen(Brushes.Blue, 6);
                Console.WriteLine("Mano derecha por debajo de la cadera.");
            }
            else if (rightHandY > hips_Y && rightHandY < shoulder_Y) //Si está entre la cadera y los hombros, se dibuja VERDE
            {
                this.trackedBonePen = new Pen(Brushes.Green, 6);
                Console.WriteLine("Mano derecha entre la cadera y los hombros.");
            }
            else if (rightHandY > shoulder_Y && rightHandY < head_Y) //Si está entre los hombros y la cabeza se dibuja AMARILLO
            {
                this.trackedBonePen = new Pen(Brushes.Yellow, 6);
                Console.WriteLine("Mano derecha entre la cabeza y los hombros.");
            }
            else if (rightHandY > head_Y) //Si está por encima de la cabeza, se dibuja ROJO
            {
                this.trackedBonePen = new Pen(Brushes.Red, 6);
                Console.WriteLine("Mano derecha por encima de la cabeza.");
            }
        }
    }
}