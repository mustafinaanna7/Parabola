using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MotionTrajectoryVisualization
{
    public partial class MainWindow : Window
    {
        private List<MotionObject> availableObjects = new List<MotionObject>()
        {
            new MotionObject("Обычная", 0, 0.47, 1.225, 0.01, 0.1, Brushes.Blue),
            new MotionObject("Жёлтая", 5, 0.8, 1.225, 0.02, 0.2, Brushes.Yellow),
            new MotionObject("Оранжевая", 10, 0.5, 1.225, 0.015, 0.15, Brushes.Orange)
        };

        private DispatcherTimer animationTimer;
        private List<ProjectileMotion.TrajectoryPoint> trajectoryPoints;
        private int currentAnimationIndex;
        private double scaleFactor = 10;
        private Polyline trajectoryLine;
        private Ellipse movingObject;

        public MainWindow()
        {
            InitializeComponent();
            cmbObject.ItemsSource = availableObjects;
            cmbObject.SelectedIndex = 0;

            animationTimer = new DispatcherTimer();
            animationTimer.Tick += AnimationTimer_Tick;
        }

        private void BtnStartSimulation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtOutput.Text = "";
                canvasTrajectory.Children.Clear();

                MotionObject selectedObject = (MotionObject)cmbObject.SelectedItem;

                double initialVelocity = double.Parse(txtInitialVelocity.Text) + selectedObject.VelocityModifier;
                double launchAngle = double.Parse(txtLaunchAngle.Text);
                double wallDistance = double.Parse(txtWallDistance.Text);

                if (launchAngle < 0 || launchAngle > 90)
                {
                    MessageBox.Show("Угол запуска должен быть в диапазоне от 0 до 90 градусов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                ProjectileMotion projectile = new ProjectileMotion(
                    initialVelocity,
                    launchAngle,
                    dragCoefficient: selectedObject.DragCoefficient,
                    airDensity: selectedObject.AirDensity,
                    projectileArea: selectedObject.ProjectileArea,
                    projectileMass: selectedObject.ProjectileMass);

                double range = projectile.GetRange();
                AddOutputText($"Дальность полёта (без учета сопротивления): {range:F2}м.");

                projectile.OnObstacleCollision += (s, args) =>
                {
                    AddOutputText($"\nСтолкновение с препятствием! X={args.CollisionX:F2}, Y={args.CollisionY:F2}");
                };

                double timeStep = 0.01;
                double maxTotalTime = 10;
                trajectoryPoints =
                    projectile.EulerMethod(timeStep, maxTotalTime, wallDistance);

                DrawStaticElements(wallDistance);
                InitializeTrajectoryLine(selectedObject.TrajectoryColor);

                AddOutputText("\nКоординаты тела в разные моменты времени (с учетом сопротивления):");
                foreach (var point in trajectoryPoints)
                {
                    AddOutputText($"t={point.Time:F2}с.; X={point.X:F2}м.; Y={point.Y:F2}м.");
                }

                StartAnimation(selectedObject.Color);
            }
            catch (FormatException)
            {
                MessageBox.Show("Пожалуйста, введите корректные числовые значения.", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DrawStaticElements(double wallDistance)
        {
            // Рисуем ось X (землю)
            var groundLine = new Line()
            {
                X1 = 0,
                Y1 = canvasTrajectory.ActualHeight - 20,
                X2 = canvasTrajectory.ActualWidth,
                Y2 = canvasTrajectory.ActualHeight - 20,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            canvasTrajectory.Children.Add(groundLine);

            // Рисуем препятствие (стену)
            double wallX = wallDistance * scaleFactor;
            var wallLine = new Line()
            {
                X1 = wallX,
                Y1 = canvasTrajectory.ActualHeight - 20,
                X2 = wallX,
                Y2 = 0,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection() { 5, 2 }
            };
            canvasTrajectory.Children.Add(wallLine);
        }

        private void InitializeTrajectoryLine(Brush color)
        {
            trajectoryLine = new Polyline()
            {
                Stroke = color,
                StrokeThickness = 1
            };
            canvasTrajectory.Children.Add(trajectoryLine);
        }

        private void StartAnimation(Brush objectColor)
        {
            if (trajectoryPoints == null || trajectoryPoints.Count == 0) return;

            currentAnimationIndex = 0;

            // Создаем движущийся объект
            movingObject = new Ellipse()
            {
                Width = 10,
                Height = 10,
                Fill = objectColor,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvasTrajectory.Children.Add(movingObject);

            animationTimer.Interval = TimeSpan.FromMilliseconds(30);
            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (currentAnimationIndex >= trajectoryPoints.Count)
            {
                animationTimer.Stop();
                return;
            }

            var point = trajectoryPoints[currentAnimationIndex];
            double x = point.X * scaleFactor;
            double y = canvasTrajectory.ActualHeight - 20 - (point.Y * scaleFactor);

            // Обновляем позицию движущегося объекта
            Canvas.SetLeft(movingObject, x - 5);
            Canvas.SetTop(movingObject, y - 5);

            // Добавляем точку в траекторию
            trajectoryLine.Points.Add(new Point(x, y));

            currentAnimationIndex++;
        }

        public void AddOutputText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(text + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }
    }

    public class MotionObject
    {
        public string Name { get; set; }
        public double VelocityModifier { get; set; }
        public double DragCoefficient { get; set; }
        public double AirDensity { get; set; }
        public double ProjectileArea { get; set; }
        public double ProjectileMass { get; set; }
        public Brush Color { get; set; }
        public Brush TrajectoryColor { get; set; }

        public MotionObject(string name, double velocityModifier, double dragCoefficient,
                          double airDensity, double projectileArea, double projectileMass, Brush color)
        {
            Name = name;
            VelocityModifier = velocityModifier;
            DragCoefficient = dragCoefficient;
            AirDensity = airDensity;
            ProjectileArea = projectileArea;
            ProjectileMass = projectileMass;
            Color = color;

            // Создаем полупрозрачную версию цвета для траектории
            TrajectoryColor = color.Clone();
            TrajectoryColor.Opacity = 0.7;
        }

        public override string ToString() => Name;
    }

    public class ProjectileMotion
    {
        public class TrajectoryPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Time { get; set; }
        }

        public double InitialVelocity { get; private set; }
        public double LaunchAngle { get; private set; }
        public double Gravity { get; private set; }
        public double DragCoefficient { get; set; }
        public double AirDensity { get; set; }
        public double ProjectileArea { get; set; }
        public double ProjectileMass { get; set; }

        public ProjectileMotion(double initialVelocity, double launchAngle, double gravity = 9.81,
                              double dragCoefficient = 0.47, double airDensity = 1.225,
                              double projectileArea = 0.01, double projectileMass = 0.1)
        {
            InitialVelocity = initialVelocity;
            LaunchAngle = launchAngle;
            Gravity = gravity;
            DragCoefficient = dragCoefficient;
            AirDensity = airDensity;
            ProjectileArea = projectileArea;
            ProjectileMass = projectileMass;
        }

        public double GetRange()
        {
            double angleInRadians = LaunchAngle * Math.PI / 180;
            return ((Math.Pow(InitialVelocity, 2) * Math.Sin(2 * angleInRadians)) / Gravity);
        }

        public List<TrajectoryPoint> EulerMethod(double timeStep, double maxTotalTime, double obstacleDistance)
        {
            List<TrajectoryPoint> trajectory = new List<TrajectoryPoint>();

            double angleInRadians = LaunchAngle * Math.PI / 180;
            double vx = InitialVelocity * Math.Cos(angleInRadians);
            double vy = InitialVelocity * Math.Sin(angleInRadians);

            double x = 0;
            double y = 0;
            double time = 0;
            bool collisionOccurred = false;
            bool landed = false;

            while (y >= 0 && time <= maxTotalTime && !collisionOccurred && !landed)
            {
                trajectory.Add(new TrajectoryPoint { X = x, Y = y, Time = time });

                if (x >= obstacleDistance)
                {
                    collisionOccurred = true;
                    OnObstacleCollision?.Invoke(this, new ObstacleCollisionEventArgs(x, y));
                    break;
                }

                double velocity = Math.Sqrt(vx * vx + vy * vy);
                double dragForce = 0.5 * DragCoefficient * AirDensity * ProjectileArea * velocity * velocity;

                double dragForceX = dragForce * (vx / velocity);
                double dragForceY = dragForce * (vy / velocity);

                double ax = -dragForceX / ProjectileMass;
                double ay = -Gravity - dragForceY / ProjectileMass;

                vx += ax * timeStep;
                vy += ay * timeStep;

                x += vx * timeStep;
                y += vy * timeStep;

                time += timeStep;

                if (y < 0)
                {
                    landed = true;
                    double timeToLand = -y / vy;
                    x -= vx * timeToLand;
                    y = 0;
                    time -= timeToLand;
                    trajectory.Add(new TrajectoryPoint { X = x, Y = y, Time = time });
                }
            }

            return trajectory;
        }

        public event EventHandler<ObstacleCollisionEventArgs> OnObstacleCollision;
    }

    public class ObstacleCollisionEventArgs : EventArgs
    {
        public double CollisionX { get; }
        public double CollisionY { get; }

        public ObstacleCollisionEventArgs(double x, double y)
        {
            CollisionX = x;
            CollisionY = y;
        }
    }
}