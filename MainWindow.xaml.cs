using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ParabolaFlightSimulator
{
    public partial class MainWindow : Window
    {
        private List<Bird> availableBirds = new List<Bird>()
        {
            new Bird("Обычная", 0, 0.47, 1.225, 0.01, 0.1),
            new Bird("Красная", 5, 0.47, 1.225, 0.01, 0.1),
            new Bird("Желтая", 10, 0.47, 1.225, 0.01, 0.15)
        };

        public MainWindow()
        {
            InitializeComponent();
            cmbBird.ItemsSource = availableBirds;
            cmbBird.SelectedIndex = 0;
        }

        private void BtnStartFlight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtOutput.Text = "";

                // Получаем выбранную птицу
                Bird selectedBird = (Bird)cmbBird.SelectedItem;

                // Считываем параметры
                double initialVelocity = double.Parse(txtInitialVelocity.Text) + selectedBird.VelocityModifier;
                double launchAngle = double.Parse(txtLaunchAngle.Text);
                double wallDistance = double.Parse(txtWallDistance.Text);

                if (launchAngle < 0 || launchAngle > 90)
                {
                    MessageBox.Show("Угол запуска должен быть в диапазоне от 0 до 90 градусов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Создаем объект для расчета траектории
                ProjectileMotion projectile = new ProjectileMotion(
                    initialVelocity,
                    launchAngle,
                    dragCoefficient: selectedBird.DragCoefficient,
                    airDensity: selectedBird.AirDensity,
                    projectileArea: selectedBird.ProjectileArea,
                    projectileMass: selectedBird.ProjectileMass);

                // Рассчитываем дальность без учета сопротивления
                double range = projectile.GetRange();
                AddOutputText($"Дальность полёта (без учета сопротивления): {range:F2}м.");

                // Устанавливаем обработчик столкновения со стеной
                projectile.OnWallCollision += (s, args) =>
                {
                    AddOutputText($"\nСтолкновение со стеной! X={args.CollisionX:F2}, Y={args.CollisionY:F2}");
                };

                // Рассчитываем траекторию
                double timeStep = 0.01;
                double maxTotalTime = 10;
                List<ProjectileMotion.TrajectoryPoint> trajectory =
                    projectile.EulerMethod(timeStep, maxTotalTime, wallDistance);

                // Выводим результаты
                AddOutputText("\nКоординаты тела в разные моменты времени (с учетом сопротивления):");
                foreach (var point in trajectory)
                {
                    AddOutputText($"t={point.Time:F2}с.; X={point.X:F2}м.; Y={point.Y:F2}м.");
                }
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

        public void AddOutputText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(text + Environment.NewLine);
                txtOutput.ScrollToEnd();
            });
        }
    }

    public class Bird
    {
        public string Name { get; set; }
        public double VelocityModifier { get; set; }
        public double DragCoefficient { get; set; }
        public double AirDensity { get; set; }
        public double ProjectileArea { get; set; }
        public double ProjectileMass { get; set; }

        public Bird(string name, double velocityModifier, double dragCoefficient,
                  double airDensity, double projectileArea, double projectileMass)
        {
            Name = name;
            VelocityModifier = velocityModifier;
            DragCoefficient = dragCoefficient;
            AirDensity = airDensity;
            ProjectileArea = projectileArea;
            ProjectileMass = projectileMass;
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

        public List<TrajectoryPoint> EulerMethod(double timeStep, double maxTotalTime, double wallDistance)
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

                // Проверка на столкновение со стеной
                if (x >= wallDistance)
                {
                    collisionOccurred = true;
                    OnWallCollision?.Invoke(this, new WallCollisionEventArgs(x, y));
                    break;
                }

                // Расчет сил сопротивления
                double velocity = Math.Sqrt(vx * vx + vy * vy);
                double dragForce = 0.5 * DragCoefficient * AirDensity * ProjectileArea * velocity * velocity;

                // Компоненты силы сопротивления
                double dragForceX = dragForce * (vx / velocity);
                double dragForceY = dragForce * (vy / velocity);

                // Ускорение с учетом сопротивления воздуха и гравитации
                double ax = -dragForceX / ProjectileMass;
                double ay = -Gravity - dragForceY / ProjectileMass;

                // Обновление скорости методом Эйлера
                vx += ax * timeStep;
                vy += ay * timeStep;

                // Обновление позиции
                x += vx * timeStep;
                y += vy * timeStep;

                time += timeStep;

                // Проверка на "приземление"
                if (y < 0)
                {
                    landed = true;
                    // Корректировка конечной позиции для точного определения точки падения
                    double timeToLand = -y / vy;
                    x -= vx * timeToLand;
                    y = 0;
                    time -= timeToLand;
                    trajectory.Add(new TrajectoryPoint { X = x, Y = y, Time = time });
                }
            }

            return trajectory;
        }

        public event EventHandler<WallCollisionEventArgs> OnWallCollision;
    }

    public class WallCollisionEventArgs : EventArgs
    {
        public double CollisionX { get; }
        public double CollisionY { get; }

        public WallCollisionEventArgs(double x, double y)
        {
            CollisionX = x;
            CollisionY = y;
        }
    }
}