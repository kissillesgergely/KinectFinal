using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

//Added namespaces
using Microsoft.Kinect;                 //To use the essentail Kinect functionalities
using System.Windows.Media.Media3D;     //To use the Point3D object
using System.IO.Ports;                  //To be able to use the serial communication
using System.Globalization;             //To adjust the decimal separation character

namespace KinectFinal
{
    public partial class Form1 : Form
    {
        public List<Point3D> virtualPoints = new List<Point3D>();
        public List<Point3D> rawPoints = new List<Point3D>();
        public List<Point3D> cleanPoints = new List<Point3D>();

        //The overall size of the physical object (approximatelly)
        public double axisDistance;
        public double modelWidth;
        public double modelHeight;
        public double baseHeight;

        //The size of the object in pixels on the recorded picture
        //These values are calculated from the physical parameters
        //58.5 x 46.6 degrees resulting in an average of about 5 x 5 pixels per degree
        public double sceneHeight;
        public double sceneWidth;
        public double sceneBase;

        public int ppp;                 //pixels per points
        public int scanlines;

        public int remainingSteps;
        public int Steps;
        public double StepSize;           //this will be maped between 0 and 255, in correspndence to the 360 degrees
        
        public KinectSensor sensor;
        public DepthImagePixel[] depthPixels;

        //To replace ',' to '.' in float and double number (because of the Hungarian default)
        NumberFormatInfo nfi = new NumberFormatInfo();

        //---------------------------------------------//

        public Form1()
        {
            InitializeComponent();
        }

        //---------------------------------------------//

        private void Form1_Load(object sender, EventArgs e)
        {
            //Find Kinect
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }
            }

            if (null != sensor)
            {
                sensor.Start();
                sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            }

            string[] ports = SerialPort.GetPortNames();
            comboBox1.Items.AddRange(ports);
            if (ports.Length>0) comboBox1.SelectedIndex = 0;
            else MessageBox.Show("Please connect the turntable and restart!",
                "No Turntable Found!", MessageBoxButtons.OK, MessageBoxIcon.Error);

            textBox1.Text = "0";
            textBox2.Text = "0";
            textBox3.Text = "0";
            textBox4.Text = "0";
            textBox5.Text = "0";

            button1.Enabled = false;
            button3.Enabled = false;

            serialPort1.ReadTimeout = 5000;

        }

        //---------------------------------------------//

        private void button1_Click(object sender, EventArgs e)
        {
            scan();
        }

        //---------------------------------------------//

        private void scan()
        {
            while (remainingSteps>0)
            {
                moveTable();
                remainingSteps--;
                Console.WriteLine(remainingSteps);
            }
            button4.Enabled = true;
        }

        //---------------------------------------------//

        private void pickUpSurfacePoints()
        {

        }

        //---------------------------------------------//

        private void calculation()
        {
            //58.5 x 46.6 degrees resulting in an average of about 5 x 5 pixels per degree
            //1.021 x 0.813 radians
            double fullWidth = 2 * Math.Tan(1.021 / 2) * axisDistance;
            double fullHeight = 2 * Math.Tan(0.813 / 2) * axisDistance;
            sceneWidth = 640 * (modelWidth / fullWidth);
            sceneHeight = 480 * (modelHeight / fullHeight);
            sceneBase = 240 + 480 * (baseHeight / fullHeight);
            StepSize = 255 / Steps;
            Console.WriteLine("Step Size: "+StepSize.ToString());
        }

        //---------------------------------------------//

        private void initilazeScan()
        {
            //Connect to the Arduino via serial port
            try
            {
                serialPort1.PortName = comboBox1.Text;
                serialPort1.Open();                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            //Read the steps of the scan, to calculate the angle of each step
            if (Convert.ToInt32(textBox1.Text) != 0)
            {
                Steps = Convert.ToInt32(textBox1.Text);
            }
            else Steps = 10;

            //Remaining steps will be decreased during the scan, but initally equal with the steps
            remainingSteps = Steps;

            //Read the distance of the axis, to calculate the middlepoint of the scan
            if (Convert.ToInt32(textBox2.Text) != 0)
            {
                axisDistance = Convert.ToInt32(textBox2.Text);
            }
            else axisDistance = 855;

            //Read the height of the model, to filter the lower and higher parts of the picture
            if (Convert.ToInt32(textBox3.Text) != 0)
            {
                modelHeight = Convert.ToInt32(textBox3.Text);
            }
            else modelHeight = 100;

            //Read the width of the model, to filter the right and left parts of the picture
            if (Convert.ToInt32(textBox4.Text) != 0)
            {
                modelWidth = Convert.ToInt32(textBox4.Text);
            }
            else modelWidth = 100;

            //Read the base of the model, to define the start point of the filtering
            if (Convert.ToInt32(textBox5.Text) != 0)
            {
                baseHeight = Convert.ToInt32(textBox5.Text);
            }
            else baseHeight = -60;

        }

        //---------------------------------------------//

        private void moveTable()
        {
            if (serialPort1.IsOpen)
            {
                char m = (char)((int)(StepSize * (Steps - remainingSteps)));
                Console.WriteLine((StepSize * (Steps - remainingSteps)).ToString());
                string m2 = m.ToString();
                //serialPort1.WriteLine("S"+ (StepSize * (Steps - remainingSteps)).ToString());
                serialPort1.WriteLine("S" + m2);
                //Console.WriteLine("S" + (StepSize * (Steps - remainingSteps)).ToString());
                Console.WriteLine("S" + m2);
                label10.Text = "S" + m2;
                Console.WriteLine("answer: "+serialPort1.ReadLine());
                Console.WriteLine("answer: "+serialPort1.ReadLine());
            }
        }

        //---------------------------------------------//

        private void exportData()
        {
            using (System.IO.StreamWriter file =
            new System.IO.StreamWriter("polygon.ply", false))
            {
                file.WriteLine("ply");
                file.WriteLine("format ascii 1.0");
                file.WriteLine("comment This is your C# ply file");
                file.WriteLine("element vertex " + Convert.ToString(virtualPoints.Count));
                file.WriteLine("property float x");
                file.WriteLine("property float y");
                file.WriteLine("property float z");
                file.WriteLine("end_header");
                for (int i = 0; i < virtualPoints.Count; i++)
                {
                    file.WriteLine((virtualPoints[i].X / 1000).ToString(nfi) + " " + (virtualPoints[i].Y / 1000).ToString(nfi)
                                    + " " + (virtualPoints[i].Z / 1000).ToString(nfi));
                }
            }
        }

        //---------------------------------------------//

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                serialPort1.Close();                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Message", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //---------------------------------------------//

        private void button4_Click(object sender, EventArgs e)
        {
            initilazeScan();
            calculation();
            button1.Enabled = true;
        }
    }
}
