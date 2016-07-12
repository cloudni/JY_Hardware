using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JYPXIE9529;

namespace Demo_JYPXIE9529AI
{
    public partial class AIDemo : Form
    {
        JYPXIE9529AITask _aiTask;      // AI操作的对象
        JYPXIE9529AOTask _aoGenSine;    //  AO操作对象，用于生成10Hz正弦信号

        double[] data = new double[4000];
        int readLen, readCount = 0;
        DateTime dt, startTime;
        bool firstOp = true;
        bool _aiStarted = false;
        int _readSamples = 0;

        public AIDemo()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗口Load事件,可以做一些初始化控件的操作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AIDemo_Load(object sender, EventArgs e)
        {
            TerminConfig.Items.AddRange(Enum.GetNames(typeof(JYPXIE9529AITask.EnumAITerminalConfig))); //根据AI端口配置的枚举给端口配置的控件添加Item
            TerminConfig.SelectedIndex = 0; //默认选择第0个端口配置            
            SampleMode.SelectedIndex = 0; //默认选择第1个采集模式

            cbChannelSelection.Items.Clear();
            for (int i = 0; i < 8; i++) //添加待选择的通道
            {
                cbChannelSelection.Items.Add(i);
            }
        }

        /// <summary>
        /// 选择板卡编号的CommboBox的选项值改变的事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeviceName_SelectedIndexChanged(object sender, EventArgs e)
        {
            _aiTask = new JYPXIE9529AITask(Convert.ToInt32(DeviceName.Text));    //根据选择的板卡编号创建AI操作的对象
            //_aiTask.AddChannel(Convert.ToInt32(cbChannelSelection.Text), "ch" + cbChannelSelection.Text, 0, 20, JYPXIE9529AITask.EnumCoupling.Default);
            _aiTask.AddChannel(Convert.ToInt32(cbChannelSelection.Text), 0, 20, JYPXIE9529AITask.EnumCoupling.Default, JYPXIE9529AITask.EnumAITerminalConfig.Default);

            _aoGenSine = new JYPXIE9529AOTask(Convert.ToInt32(DeviceName.Text)); //根据选择的板卡编号创建AO操作的对象          
            _aoGenSine.ClearChannels(); //初始化生成正弦信号的AO通道
            _aoGenSine.AddChannel(0, "ch0", 0, 5);
        }

        /// <summary>
        /// 退出按钮单击事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Quit_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要退出吗？", "提示确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                this.Close();
            }
        }

        /// <summary>
        /// AO通道0是否输出10Hz正弦信号
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (_aoGenSine == null)
            {
                return;
            }

            if (checkBox1.Checked == true)
            {
                _aoGenSine.Mode = JYPXIE9529AOTask.EnumAOMode.ContinuousNoWrapping;
                //_aoGenSine.EnableWrapping = true;

                //***********生成正弦信号************
                int samples = 51200;
                double freq = 13;
                double updateRate = 51200;
                double[,] buf = new double[samples, 1];
                for (int i = 0; i < samples; i++)
                {
                    buf[i, 0] = (1 + Math.Sin(2 * Math.PI * freq * i / updateRate)) * 2.5;
                }
                //***********************************
                _aoGenSine.UpdateRate = updateRate;

                //_aoGenSine.WriteData(buf, out samples, -1);
                _aoGenSine.WriteData(buf, -1);

                _aoGenSine.Start();
            }
            else
            {
                _aoGenSine.Stop();
            }
        }

        /// <summary>
        /// 采集模式改变处理事件，主要针对不同采集模式是改变相关控件的状态
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SampleMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            SampleRate.Enabled = true;
            SampleRate.PasswordChar = '\0';

            SampleCount.Enabled = true;
            SampleCount.PasswordChar = '\0';

            if (SampleMode.SelectedIndex + 1 == (int)JYPXIE9529AITask.EnumAIMode.Continuous)
            {
                SampleCount.Enabled = false;
                SampleCount.PasswordChar = '-';
            }
            else if (SampleMode.SelectedIndex + 1 == (int)JYPXIE9529AITask.EnumAIMode.Single)
            {
                SampleCount.Enabled = false;
                SampleRate.Enabled = false;

                SampleCount.PasswordChar = '-';
                SampleRate.PasswordChar = '-';

            }
        }

        /// <summary>
        /// 程序退出做一些清理工作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AIDemo_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_aiStarted)
            {
                _aiTask.Stop();
            }
            if (checkBox1.Checked == true)
            {
                _aoGenSine.Stop();
            }
        }

        private void AcquireAction_Click(object sender, EventArgs e)
        {
            if (_aiTask != null)
            {
                JYPXIE9529AITask.AIChnParam channel = _aiTask.Channels[0];
                //检查AI任务中的通道ID是否与当前配置的一致，如果不一致则重新添加
                if (channel.ChnID != cbChannelSelection.SelectedIndex)
                {
                    _aiTask.Channels.Clear();
                    //_aiTask.AddChannel(Convert.ToInt32(cbChannelSelection.Text), channel.ChnID, channel.RangeLow, channel.RangeHi, JYPXIE9529AITask.EnumCoupling.DC);
                    _aiTask.AddChannel(Convert.ToInt32(cbChannelSelection.Text), channel.RangeLow, channel.RangeHi, JYPXIE9529AITask.EnumCoupling.DC, JYPXIE9529AITask.EnumAITerminalConfig.Default);
                }

                _aiTask.SampleRate = double.Parse(SampleRate.Text);  //配置采样率
                if (_aiTask.DSA_ConfigSpeedRate() < 0)
                {
                    throw new Exception("初始化失败，请检查Speed Rate设置！");
                }

                foreach(JYPXIE9529AITask.AIChnParam ch in _aiTask.Channels)
                {
                    if (_aiTask.DSA_AI_ConfigChannel((ushort)ch.ChnID) < 0)
                    {
                        throw new Exception("DSA_AI_9529_ConfigChannel Falied");
                    }
                }

                if (_aiTask.DSA_TRG_Config() < 0)
                {
                    throw new Exception("DSA_TRG_Config Falied");
                }

                _aiTask.Mode = (JYPXIE9529AITask.EnumAIMode)(SampleMode.SelectedIndex + 1); //配置采集模式
                switch (_aiTask.Mode)
                {
                    case JYPXIE9529AITask.EnumAIMode.Continuous:
                        ContinuousAcquireControl();
                        break;
                    case JYPXIE9529AITask.EnumAIMode.Finite:
                        FiniteAcquireControl();
                        break;
                    case JYPXIE9529AITask.EnumAIMode.Single:
                        break;
                    default:
                        break;
                }
            }
            else
            {
                MessageBox.Show("请首先选择板卡编号！");
                return;
            }
        }

        /// <summary>
        /// HHHHHHHHHHHHHHHHHHH
        /// </summary>
        private void ContinuousAcquireControl()
        {
            if (_aiStarted == false) //ai 采集未启动
            {
                _aiTask.Start();

                ContSamplesTimer.Enabled = true;

                _aiStarted = true; //标识AI采集已经开始
                AcquireAction.Image = Properties.Resources.stop; //切换Start按钮的图标
            }
            else //采集在进行中
            {
                _aiTask.Stop(); //停止AI采集任务

                AcquireAction.Image = Properties.Resources.runit; //切换Start按钮的图标
                ContSamplesTimer.Enabled = false;
                _aiStarted = false;
            }
        }

        private void FiniteAcquireControl()
        {
            _aiTask.SamplesPerChannel = int.Parse(SampleCount.Text);

            _aiTask.Start();

            AcquireAction.Image = Properties.Resources.stop; //切换Start按钮的图标

            double[,] data = new double[_aiTask.SamplesPerChannel, 1];
            //从任务缓冲区读取采集到的数据
            //_aiTask.ReadData(data, _aiTask.SamplesPerChannel, out readLen, -1);
            _aiTask.ReadData(ref data, _aiTask.SamplesPerChannel, System.Convert.ToBoolean(readLen), -1);
            Display(data);
            _aiTask.Stop();

            AcquireAction.Image = Properties.Resources.runit; //切换Start按钮的图标
        }

        private void ContSamplesTimer_Tick(object sender, EventArgs e)
        {
            ContSamplesTimer.Enabled = false;
            int len = (int)_aiTask.SampleRate / 2;
            double[,] data = new double[len, 1];
            //从任务缓冲区读取采集到的数据
            //_aiTask.ReadData(data, len, out readLen, -1);
            _aiTask.ReadData(ref data, len, System.Convert.ToBoolean(readLen), -1);
            Display(data);
            ContSamplesTimer.Enabled = true;
        }

        /// <summary>
        /// 单点采集时，读取数据的定时器事件函数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SingleReadTimer_Tick(object sender, EventArgs e)
        {
            _aiTask.ReadSinglePoint(ref data);
            if (firstOp) //如果是第一次启动则清除Chart的所有数据，并设置波形的颜色为蓝色
            {
                WaveChart.Series[0].Points.Clear();
                WaveChart.Series[0].Color = Color.Blue;
            }
            firstOp = false; //终止“第一次启动标识”
            WaveChart.Series[0].Points.AddY(data[0]); //新增显示数据
        }

        private double GetMax()
        {
            return 0;
        }

        private void Display(double[,] data)
        {
            readCount += readLen;
            if (readLen > 0)
            {
                double[] data1 = new double[readLen];
                Buffer.BlockCopy(data, 0, data1, 0, readLen * sizeof(double));
                RefreshWaveForm(data1.Take(readLen).ToArray());
            }
            string s = string.Format("{0}", (DateTime.Now - startTime).TotalMilliseconds / 1000.0);

        }

        /// <summary>
        /// 用输入的数据刷新波形控件的波形显示
        /// </summary>
        /// <param name="readdata"></param>
        private void RefreshWaveForm(double[] readdata)
        {
            WaveChart.Series[0].Points.DataBindY(readdata, "");
        }
    }
}
