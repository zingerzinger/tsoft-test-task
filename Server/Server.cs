using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using System.Threading;
using System.Threading.Tasks;

using System.Diagnostics;

using Grpc.Core;
using Proto;

namespace Srv
{

    static class COMMON
    {
        public const int NET_PORT = 7777;

        public const int NUM_BODIES_NET_CHOKE = 32;

        public const float WORLD_WIDTH  = 640.0f;
        public const float WORLD_HEIGHT = 640.0f;

        public const int NUM_BODIES = 64;
        public const int NUM_TASKS  = 4;

        public const int PHYSICS_DT_MSECS = 1000 / 60;
        public const float BODY_SIZE = 20.0f;
        public const float BODY_VELOCITY = 300.0f / (1000.0f / PHYSICS_DT_MSECS);

        public static List<Body> bodies = new List<Body>();
    }

    enum MovementDirection {
        RIGHTUP   = 0,
        RIGHTDOWN,
        LEFTUP   ,
        LEFTDOWN ,

        NUMDIRECTIONS
    }

    struct Body {

        public float x; // top left corner
        public float y;

        public MovementDirection direction;

        public Body(float x, float y, MovementDirection direction) {
            this.x = x;
            this.y = y;

            this.direction = direction;
        }

    }

    class Task
    {
        Thread thread;
        bool running;

        public Task(int idxStart, int idxFinish, List<Body> bodies) {
            running = false;
            this.thread = new Thread(() => Process(idxStart, idxFinish, bodies, this));
        }

        static void Process(int idxStart, int idxFinish, List<Body> bodies, Task task) {

            while (task.running) {

                Thread.Sleep(COMMON.PHYSICS_DT_MSECS);

                for (int i = idxStart; i <= idxFinish; i++) {

                    Body body = bodies[i];

                    bool top    = false;
                    bool right  = false;
                    bool bottom = false;
                    bool left   = false;

                    switch (body.direction) {
                        case MovementDirection.RIGHTUP  : {

                            body.x += COMMON.BODY_VELOCITY;
                            body.y -= COMMON.BODY_VELOCITY;

                            if (body.x + COMMON.BODY_SIZE > COMMON.WORLD_WIDTH) { right = true; body.x = COMMON.WORLD_WIDTH - COMMON.BODY_SIZE; }
                            if (body.y                    <                  0) { top   = true; body.y = 0; }

                            if      (top && right) { body.direction = MovementDirection.LEFTDOWN;  }
                            else if (top         ) { body.direction = MovementDirection.RIGHTDOWN; }
                            else if (right       ) { body.direction = MovementDirection.LEFTUP;    }

                        } break;

                        case MovementDirection.RIGHTDOWN: {
                            body.x += COMMON.BODY_VELOCITY;
                            body.y += COMMON.BODY_VELOCITY;

                            if (body.x + COMMON.BODY_SIZE > COMMON.WORLD_WIDTH ) { right  = true; body.x = COMMON.WORLD_WIDTH  - COMMON.BODY_SIZE; }
                            if (body.y + COMMON.BODY_SIZE > COMMON.WORLD_HEIGHT) { bottom = true; body.y = COMMON.WORLD_HEIGHT - COMMON.BODY_SIZE; }

                            if      (bottom && right) { body.direction = MovementDirection.LEFTUP;   }
                            else if (bottom         ) { body.direction = MovementDirection.RIGHTUP;  }
                            else if (right          ) { body.direction = MovementDirection.LEFTDOWN; }
                        } break;

                        case MovementDirection.LEFTUP   : {
                            body.x -= COMMON.BODY_VELOCITY;
                            body.y -= COMMON.BODY_VELOCITY;

                            if (body.x < 0) { left = true; body.x = 0; }
                            if (body.y < 0) { top  = true; body.y = 0; }

                            if      (top && left) { body.direction = MovementDirection.RIGHTDOWN; }
                            else if (top        ) { body.direction = MovementDirection.LEFTDOWN ; }
                            else if (left       ) { body.direction = MovementDirection.RIGHTUP  ; }
                        } break;

                        case MovementDirection.LEFTDOWN : {
                            body.x -= COMMON.BODY_VELOCITY;
                            body.y += COMMON.BODY_VELOCITY;

                            if (body.x                    < 0                  ) { left   = true; body.x = 0; }
                            if (body.y + COMMON.BODY_SIZE > COMMON.WORLD_HEIGHT) { bottom = true; body.y = COMMON.WORLD_HEIGHT - COMMON.BODY_SIZE; }

                            if      (bottom && left) { body.direction = MovementDirection.RIGHTUP  ; }
                            else if (bottom        ) { body.direction = MovementDirection.LEFTUP   ; }
                            else if (left          ) { body.direction = MovementDirection.RIGHTDOWN; }
                        } break;
                    }

                    bodies[i] = body;
                }
            }
        }

        public void Start() { running = true; thread.Start(); }
        public void Stop() { running = false; }
    }

    class Host
    {

        List<Task> tasks = new List<Task>();
        Random rand = new Random((int)Stopwatch.GetTimestamp());

        public Host() {

            for (int i = 0; i < COMMON.NUM_BODIES; i++) {
                Body b = new Body(rand.Next(0, (int)(COMMON.WORLD_WIDTH  - COMMON.BODY_SIZE)),
                                  rand.Next(0, (int)(COMMON.WORLD_HEIGHT - COMMON.BODY_SIZE)),
                                  (MovementDirection)rand.Next(0, (int) MovementDirection.NUMDIRECTIONS ));
                COMMON.bodies.Add(b);
            }

            int bodiesPerTask = COMMON.NUM_BODIES / COMMON.NUM_TASKS;
            int remainder     = COMMON.NUM_BODIES % COMMON.NUM_TASKS;

            for (int i = 0; i < COMMON.NUM_TASKS; i++) {
                //Console.WriteLine("{0} : {1}", i * bodiesPerTask, i * bodiesPerTask + bodiesPerTask - 1);
                Task t = new Task(i * bodiesPerTask, i * bodiesPerTask + bodiesPerTask - 1, COMMON.bodies); tasks.Add(t); t.Start();
            }

            if (remainder > 0) {
                //Console.WriteLine("{0} : {1}", bodies.Count - remainder, bodies.Count - 1);
                Task t = new Task(COMMON.bodies.Count - remainder, COMMON.bodies.Count - 1, COMMON.bodies); tasks.Add(t); t.Start();
            }
        }
    }

    public class GrpcServer : Svc.SvcBase
    {
        public override async Task<BodyData> BodyDataService(Request request, IServerStreamWriter<BodyData> responseStream, ServerCallContext context) {
 
            Console.WriteLine("New client");

            int idx = 0;
            int bodiesCounter = 0;

            while (true) {
                Body body = COMMON.bodies[idx];
                await responseStream.WriteAsync(new BodyData { Id = idx, X = body.x, Y = body.y });
                idx = (idx + 1) % COMMON.bodies.Count;

                bodiesCounter++;
                if (bodiesCounter >= COMMON.NUM_BODIES_NET_CHOKE) {
                    bodiesCounter = 0;
                    Thread.Sleep(1);
                }
            }
        }
    }

    class Program
    {
        static void Main(string[] args) {

            Host host = new Host();

            Server server = new Server {
                Services = { Svc.BindService(new GrpcServer()) },
                Ports    = { new ServerPort("0.0.0.0", COMMON.NET_PORT, ServerCredentials.Insecure) }
            };

            server.Start();

            Console.ReadKey();
        }
    }

}