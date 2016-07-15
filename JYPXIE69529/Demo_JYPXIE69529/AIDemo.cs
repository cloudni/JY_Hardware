using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using JYPXIE69529;

namespace Demo_JYPXIE69529
{
    public partial class AIDemo : Form
    {
        JYPXIE69529AITask _aiTask;      // AI操作的对象

        bool _aiStarted = false;

        public AIDemo()
        {
            InitializeComponent();
        }

        private void AIDemo_Load(object sender, EventArgs e)
        {

        }

        private void DeviceName_SelectedIndexChanged(object sender, EventArgs e)
        {
            _aiTask = new JYPXIE69529AITask(Convert.ToInt32(DeviceName.Text));    //根据选择的板卡编号创建AI操作的对象
            _aiTask.AddChannel(Convert.ToInt32(cbChannelSelection.Text), 0, 20, JYPXIE69529AITask.EnumCoupling.Default, JYPXIE69529AITask.EnumAITerminalConfig.Default);
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

            if (SampleMode.SelectedIndex + 1 == (int)JYPXIE69529AITask.EnumAIMode.Continuous)
            {
                SampleCount.Enabled = false;
                SampleCount.PasswordChar = '-';
            }
            else if (SampleMode.SelectedIndex + 1 == (int)JYPXIE69529AITask.EnumAIMode.Single)
            {
                SampleCount.Enabled = false;
                SampleRate.Enabled = false;

                SampleCount.PasswordChar = '-';
                SampleRate.PasswordChar = '-';

            }
        }

        private void AcquireAction_Click(object sender, EventArgs e)
        {
            if (_aiTask != null)
            {
                JYPXIE69529AITask.AIChnParam channel = _aiTask.Channels[0];
                //检查AI任务中的通道ID是否与当前配置的一致，如果不一致则重新添加
                if (channel.ChnID != cbChannelSelection.SelectedIndex)
                {
                    _aiTask.Channels.Clear();
                    _aiTask.AddChannel(Convert.ToInt32(cbChannelSelection.Text), channel.RangeLow, channel.RangeHi, JYPXIE69529AITask.EnumCoupling.Default, JYPXIE69529AITask.EnumAITerminalConfig.Default);
                }

                _aiTask.SampleRate = double.Parse(SampleRate.Text);  //配置采样率
                _aiTask.Mode = (JYPXIE69529AITask.EnumAIMode)(SampleMode.SelectedIndex + 1); //配置采集模式

                switch (_aiTask.Mode)
                {
                    case JYPXIE69529AITask.EnumAIMode.Continuous:
                        ContinuousAcquireControl();
                        break;
                    case JYPXIE69529AITask.EnumAIMode.Finite:
                        //FiniteAcquireControl();
                        break;
                    case JYPXIE69529AITask.EnumAIMode.Single:
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
    }
}
