using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int maximumApproaches = 6;
            int shootersNumber = 13;
            List<Shooter> shooters = new List<Shooter>();
            for (int i = 0; i < shootersNumber; i++)
                shooters.Add(new Shooter(name: i.ToString(), maximumApproaches));
            List<Instructor> instructors = new List<Instructor>();
            int instructorsNumber = 2;
            for (int i = 0; i < instructorsNumber; i++)
                instructors.Add(new Instructor(name: i.ToString()));
            int directionsNumber = 6;
            List<Direction> directions = new List<Direction>();
            for (int i = 0; i < directionsNumber; i++)
                directions.Add(new Direction(name: i.ToString(), instructors[i % instructorsNumber]));

            try
            {
                StartShooting(shooters, instructors, directions).Wait();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.Read();
        }

        public static async Task StartShooting(List<Shooter> shooters, List<Instructor> instructors, List<Direction> directions)
        {
            if (shooters == null || instructors == null || directions == null)
                throw new ArgumentNullException();

            Queue<Shooter> shootersQueue = new Queue<Shooter>(shooters);
            List<Task> tasks = new List<Task>(), shootingTasks = new List<Task>();
            int seconds = 0, shootingSeconds = 0;
            Random random = new Random();

            do
            {
                if (tasks.Any())
                {
                    Task finishedTask = await Task.WhenAny(tasks);
                    tasks.Remove(finishedTask);
                    if (finishedTask is Task<Message>)
                    {
                        Message message = await (Task<Message>)finishedTask;
                        Console.WriteLine($"Направление {message.Direction}, инструктор {message.Instructor}, стрелок {message.Shooter}: {message.Text}");
                        seconds += message.Seconds;
                        if (shootingTasks.Contains(finishedTask))
                            shootingSeconds += message.Seconds;
                    }
                }

                var freeDirections = directions.FindAll(x => x.IsFree());
                while (freeDirections.Any() && shootersQueue.Any())
                {
                    Shooter shooter = shootersQueue.Dequeue();
                    Direction direction = freeDirections[random.Next(0, freeDirections.Count)];
                    freeDirections.Remove(direction);
                    List<Task> newTasks = new List<Task>();
                    newTasks.Add(shooter.TakeDirection(direction));
                    newTasks.Add(newTasks[0].ContinueWith(x => direction.Instructor.PlanDirection(direction)));
                    tasks.AddRange(newTasks);
                }

                foreach (Instructor instructor in instructors.FindAll(x => x.ReadyToTakeDirection()))
                {
                    instructor.TakeDirection();
                    Direction direction = instructor.ActiveDirection;
                    Shooter shooter = direction.Shooter;
                    List<Task> newTasks = new List<Task>();
                    newTasks.Add(instructor.AllowPreparation(direction));
                    newTasks.Add(newTasks[0].ContinueWith(x => shooter.PrepareToShoot(direction)).Unwrap());
                    newTasks.Add(newTasks[1].ContinueWith(x => instructor.AllowShooting(direction)).Unwrap());
                    newTasks.Add(newTasks[2].ContinueWith(x => shooter.Shoot(direction)).Unwrap());
                    shootingTasks.Add(newTasks.Last());
                    newTasks.Add(newTasks[3].ContinueWith(x =>
                    {
                        shooter.LeaveDirection(direction);
                        instructor.LeaveDirection(direction);
                        if (shooter.Approaches < shooter.MaximumApproaches)
                            shootersQueue.Enqueue(shooter);
                    }));
                    tasks.AddRange(newTasks);
                }
            }
            while (tasks.Any());

            Console.WriteLine($"Сумма затраченного времени: {seconds}. Длительность стрельб: {shootingSeconds}");
        }

        public struct Message
        {
            public readonly string Direction, Instructor, Shooter, Text;
            public readonly int Seconds;

            public Message(string direction, string instructor, string shooter, string text, int seconds)
            {
                Direction = direction;
                Instructor = instructor;
                Shooter = shooter;
                Text = text;
                Seconds = seconds;
            }
        }

        public class Direction
        {
            public readonly string Name;
            public Shooter Shooter;
            public readonly Instructor Instructor;

            public Direction(string name, Instructor instructor)
            {
                Name = name;
                Shooter = null;
                Instructor = instructor;
            }
            public bool IsFree()
            {
                return Shooter == null;
            }
        }

        public class Shooter
        {
            public readonly string Name;
            public readonly int MaximumApproaches;

            public int Approaches { get; protected set; }

            public Shooter(string name, int maximumApproaches)
            {
                Name = name;
                Approaches = 0;
                MaximumApproaches = maximumApproaches;
            }
            public async Task<Message> TakeDirection(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                direction.Shooter = this;
                int seconds = (new Random()).Next(3, 11);
                await Task.Delay(new TimeSpan(0, 0, seconds));
                return new Message(direction.Name, direction.Instructor.Name, this.Name, "Занял направление!", seconds);
            }
            public async Task<Message> PrepareToShoot(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                int seconds = (new Random()).Next(2, 7);
                await Task.Delay(new TimeSpan(0, 0, seconds));
                return new Message(direction.Name, direction.Instructor.Name, this.Name, "К стрельбе готов!", seconds);
            }
            public async Task<Message> Shoot(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                int seconds = (new Random()).Next(5, 16);
                await Task.Delay(new TimeSpan(0, 0, seconds));
                Approaches++;
                return new Message(direction.Name, direction.Instructor.Name, this.Name, "Стрельбу окончил!", seconds);
            }
            public void LeaveDirection(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                direction.Shooter = null;
            }
        }

        public class Instructor
        {
            public string Name;
            public Direction ActiveDirection { get; protected set; }
            private Queue<Direction> directionsQueue;
            public Instructor(string name)
            {
                this.Name = name;
                ActiveDirection = null;
                directionsQueue = new Queue<Direction>();
            }

            public void PlanDirection(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                directionsQueue.Enqueue(direction);
            }
            public bool ReadyToTakeDirection()
            {
                return ActiveDirection == null && directionsQueue.Any();
            }

            public void TakeDirection()
            {
                ActiveDirection = directionsQueue.Dequeue();
            }

            public async Task<Message> AllowPreparation(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                ActiveDirection = direction;
                int seconds = (new Random()).Next(2, 7);
                await Task.Delay(new TimeSpan(0, 0, seconds));
                return new Message(direction.Name, this.Name, direction.Shooter.Name, "Подготовиться к стрельбе!", seconds);
            }
            public async Task<Message> AllowShooting(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                int seconds = (new Random()).Next(1, 3);
                await Task.Delay(new TimeSpan(0, 0, seconds));
                return new Message(direction.Name, this.Name, direction.Shooter.Name, "Произвести стрельбу!", seconds);
            }
            public void LeaveDirection(Direction direction)
            {
                if (direction == null)
                    throw new ArgumentNullException();
                direction.Instructor.ActiveDirection = null;
            }
        }
    }
}
