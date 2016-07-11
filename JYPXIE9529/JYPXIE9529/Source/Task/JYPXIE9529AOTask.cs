using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JYCommon;

namespace JYPXIE9529
{
    /// <summary>
    /// AO输出任务类,是Sealed类，不可被继承
    /// </summary>
    public sealed class JYPXIE9529AOTask
    {
        /// <summary>
        /// 构造函数,仅输入板卡编号
        /// </summary>
        /// <param name="boardNum">板卡的编号</param>
        public JYPXIE9529AOTask(int boardNum)
        {
            //获取板卡操作类的实例
            _devHandle = JYPXIE9529Device.GetInstance((ushort)boardNum);
            if (_devHandle == null)
            {
                throw new Exception("初始化失败，请检查board number或硬件连接！");
            }
            _adjustedUpdateRate = 1000;//GetRealAcqRate(updateRate); //根据需要的采样率获取真实的采样率
                                             //= samplesToUpdate; //每通道采样点数
            _updateMode =  EnumAOMode.Finite; //更新模式
            _samplesToUpdate = 1000;

            _channels = new List<AOChnParam>();            

            _clkSrc = EnumAOClkSrc.Internal;
            _clkEdge = EnumAOClkEdge.Rising;
            _triggerParam = new CAOTriggerParam();
            _triggerParam.TriggerType = EnumAOTriggerType.Immediate;

            _bufLenInSamples = 0; //默认为0，任务启动的时候如果用户没有配置过就根据采样率设置为缓冲10s

            _localBuffer = new Queue<ushort>(); //本地软件缓冲区设置

            _waitUntilDoneEvent = new WaitEvent(() => _taskDone);
            _eventQueue = new Queue<WaitEvent>(8);

            _AI_EnableIEPE = false;
        }

        #region -----------------私有字段------------------
        //添加需要使用的私有属性字段

        /// <summary>
        /// 操作硬件的对象
        /// </summary>
        private JYPXIE9529Device _devHandle;

        /// <summary>
        /// AO是否已启动
        /// </summary>
        private bool _aoStarted;

        /// <summary>
        /// 本地缓冲队列
        /// </summary>
        private Queue<ushort> _localBuffer;

        private Thread _thdWriteData;

        /// <summary>
        /// 任务结束标志 
        /// </summary>
        private bool _taskDone;
        private bool TaskDone
        {
            get
            {
                return _taskDone;
            }
            set
            {
                _taskDone = value;
                if (_taskDone)//&& _waitUntilDoneEvent.ConditionHandler()
                {
                    _waitUntilDoneEvent.Set();
                    _aoStarted = false;
                }
                else if (Mode != EnumAOMode.Single)
                {
                    _aoStarted = true;
                }

            }

        }

        /// <summary>
        /// WaitUntilDone等待事件
        /// </summary>
        private WaitEvent _waitUntilDoneEvent;

        /// <summary>
        /// 本地缓冲区中每通道的样点数
        /// </summary>
        private int _sampleCountInBuffer;

        /// <summary>
        /// 等待锁, 用于限制多线程并行写操作. 需要等一个线程读取完成后, 另一个线程才能读(排队).
        /// </summary>
        private StatusLock _startedLock = new StatusLock(); // 控制对_buffer的修改

        /// <summary>
        /// 等待锁, 用于限制多线程并行写操作. 需要等一个线程写入完成后, 另一个线程才能写(排队).
        /// </summary>
        private object _waitLock = new object();

        private Queue<WaitEvent> _eventQueue;
        /// <summary>
        /// 事件队列。调用WaitUntilDone()或者ReadBuffer()时，使用事件通知方式，提高效率。
        /// </summary>
        private Queue<WaitEvent> EventQueue
        {
            get { return _eventQueue; }
        }

        /// <summary>
        /// 缓冲区下溢出标志
        /// </summary>
        private bool _isUnderflow;

        private bool _AI_EnableIEPE;

        #endregion

        #region -------------------公共属性---------------------
        private List<AOChnParam> _channels;
        /// <summary>
        /// 通道列表
        /// </summary>
        public List<AOChnParam> Channels
        {
            get { return _channels; }
        }

        private EnumAOMode _updateMode;
        /// <summary>
        /// 输出模式，支持Single/Finite/Continuous三种类型
        /// </summary>
        public EnumAOMode Mode
        {
            get { return _updateMode; }
            set
            {
                _updateMode = value;
            }
        }

        private double _adjustedUpdateRate;
        /// <summary>
        /// 每通道更新速率
        /// </summary>
        /// <remarks>若硬件不支持硬件定时，则忽略设置的速率值</remarks>
        public double UpdateRate
        {
            get { return _adjustedUpdateRate; }
            set
            {
                //需要根据分频系数反算真实采样率
                _adjustedUpdateRate = value;
                _bufLenInSamples = (int)(value * 10);
            }
        }

        private int _samplesToUpdate;
        /// <summary>
        /// 有限点采集时, 每通道采集的样点数。若设置为小于0，则采集无穷个点。
        /// <para>默认值为256</para>
        /// </summary>
        public int SamplesToUpdate
        {
            get { return _samplesToUpdate; }
            set
            {
                _samplesToUpdate = value;
            }
        }

        private int _bufLenInSamples;
        /// <summary>
        /// 缓冲区能容纳的每通道样点数。一次读取的样点数不能超过此容量。        
        /// <remarks>在调用 Start() 方法后分配或者调整缓冲区。</remarks>
        public int BufLenInSamples
        {
            get
            {
                if (_bufLenInSamples == 0)
                {
                    _bufLenInSamples = (int)(UpdateRate * 10);
                    return _bufLenInSamples;
                }
                else
                {
                    return _bufLenInSamples;
                }
            }
            set
            {
                Interlocked.Exchange(ref _bufLenInSamples, value);
            }
        }

        /// <summary>
        /// 缓冲区当前每通道可容纳的样点数（当前每通道可写入缓冲区的样点数）
        /// </summary>
        public int AvaliableBufferPerChannel
        {
            get
            {
                //需要根据程序运行的实际情况及相关变量得到
                return _bufLenInSamples - _localBuffer.Count / _channels.Count;
            }
        }

        private EnumAOClkSrc _clkSrc;
        /// <summary>
        /// 时钟源
        /// </summary>
        public EnumAOClkSrc ClkSrc
        {
            get { return _clkSrc; }
            set
            {
                _clkSrc = value;
            }
        }

        private EnumAOClkEdge _clkEdge;
        /// <summary>
        /// 时钟沿。仅在外部时钟时有效。
        /// </summary>
        public EnumAOClkEdge ClkEdge
        {
            get { return _clkEdge; }
            set
            {
                _clkEdge = value;
            }
        }

        private CAOTriggerParam _triggerParam;
        /// <summary>
        /// AO触发参数配置
        /// </summary>
        public CAOTriggerParam TriggerParam
        {
            get { return _triggerParam; }
            set { _triggerParam = value; }
        }

        public bool AI_EnableIEPE
        {
            get { return _AI_EnableIEPE; }
            set { _AI_EnableIEPE = value; }
        }
        #endregion               

        #region --------------公共方法定义-----------------
        /// <summary>
        /// 添加通道
        /// </summary>
        /// <param name="chnID">通道物理序号</param>
        /// <param name="chnName">通道名称</param>
        /// <param name="rangeLow">通道量程下限</param>
        /// <param name="rangeHi">通道量程上限</param>
        public int AddChannel(int chnID, string chnName, double rangeLow, double rangeHi)
        {
            //To Add 添加通道的代码  

            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 添加通道
        /// </summary>
        /// <param name="chnID">要添加通道的所有物理序号</param>
        /// <param name="chnName">通道名称</param>
        /// <param name="rangeLow">通道量程下限</param>
        /// <param name="rangeHi">通道量程上限</param>
        public int AddChannel(int[] chnID, string chnName, double rangeLow, double rangeHi)
        {
            //To Add 添加通道的代码  

            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 删除指定通道号的通道
        /// </summary>
        /// <param name="chnID">要删除的通道的通道号</param>
        public int RemoveChannel(int chnID)
        {
            //To Add 删除通道的代码  

            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 将数据写入到缓冲区
        /// </summary>
        /// <param name="Buf">要写入的数据，每通道按列存放</param>
        /// <param name="timeout">操作时间</param> 
        public int WriteData(double[,] Buf, int timeout)
        {            
            short[,] TmpBuf = new short[Buf.GetLength(0), Buf.GetLength(1)];
            //To Add: Scale To Raw Data
            //Volt2RawData(Buf, ref TmpBuf);
            return WriteRawData(TmpBuf, timeout);
        }

        /// <summary>
        /// 将数据写入到缓冲区
        /// </summary>
        /// <param name="Buf">要写入的数据，多通道时数据是按行交错存放</param>
        /// <param name="timeout">操作时间</param> 
        public int WriteData(double[] Buf, int timeout)
        {
            short[,] TmpBuf = new short[Buf.GetLength(0), Buf.GetLength(1)];
            //To Add: Scale To Raw Data
            //Volt2RawData(Buf, ref TmpBuf);
            return WriteRawData(TmpBuf, timeout);
        }

        /// <summary>
        /// 将原始数据写入到缓冲区
        /// </summary>
        /// <param name="Buf">要写入的数据，每通道按列存放</param>
        /// <param name="timeout">操作时间</param>
        /// <returns></returns>
        public int WriteRawData(short[,] Buf, int timeout)
        {
            if (Mode == EnumAOMode.Single)
            {
                return JYErrorCode.CannotCall;
            }
            else if (_taskDone && AvaliableBufferPerChannel < SamplesToUpdate)
            {
                return JYErrorCode.IncorrectCallOrder;
            }

            if (Mode == EnumAOMode.Finite && SamplesToUpdate <= 0)
            {
                return JYErrorCode.ErrorParam; //数组长度不够，确保
            }
            if ((Buf.GetLength(1) < _channels.Count || Mode == EnumAOMode.Finite && Buf.GetLength(0) < SamplesToUpdate))
            {
                return JYErrorCode.UserBufferError;
            }

            if (Buf.GetLength(0) > BufLenInSamples)
            {
                _bufLenInSamples = Buf.GetLength(0); //预分配缓冲区
            }

            lock (_startedLock)
            {
                //To Add: 如果是第一次写入，则写入本地缓冲区，在Start时刻使用

            }
            

            bool isTimeout = false;
            lock (_waitLock) //防止多个线程同时写入；要求“排队”写入。
            {
                //To Add: 等待缓冲区内的数据足够之后进行读取

            }
            if (_isUnderflow)
            {
                _isUnderflow = false;
                return JYErrorCode.BufferDownflow;
            }
                        
            if (isTimeout)
            {
                return JYErrorCode.TimeOut;
            }
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 将原始数据写入到缓冲区
        /// </summary>
        /// <param name="Buf">要写入的数据，多通道时，数据是按行交错存放</param>
        /// <param name="timeout">操作时间</param>
        /// <returns></returns>
        public int WriteRawData(short[] Buf, int timeout)
        {
            if (Mode == EnumAOMode.Single)
            {
                return JYErrorCode.CannotCall;
            }
            else if (_taskDone && AvaliableBufferPerChannel < SamplesToUpdate)
            {
                return JYErrorCode.IncorrectCallOrder;
            }

            if (Mode == EnumAOMode.Finite && SamplesToUpdate <= 0)
            {
                return JYErrorCode.ErrorParam; //数组长度不够，确保
            }
            if ((Buf.GetLength(1) < _channels.Count || Mode == EnumAOMode.Finite && Buf.GetLength(0) < SamplesToUpdate))
            {
                return JYErrorCode.UserBufferError;
            }

            if (Buf.GetLength(0) > BufLenInSamples)
            {
                _bufLenInSamples = Buf.GetLength(0); //预分配缓冲区
            }

            lock (_startedLock)
            {
                //To Add: 如果是第一次写入，则写入本地缓冲区，在Start时刻使用

            }


            bool isTimeout = false;
            lock (_waitLock) //防止多个线程同时写入；要求“排队”写入。
            {
                //To Add: 等待缓冲区内的数据足够之后进行读取

            }
            if (_isUnderflow)
            {
                _isUnderflow = false;
                return JYErrorCode.BufferDownflow;
            }

            if (isTimeout)
            {
                return JYErrorCode.TimeOut;
            }
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 每通道更新一个点。直接更新，不经过缓冲区。
        /// </summary>
        /// <param name="buf">用户缓冲区数组, 数组大小须不小于<see cref="NumOfChns"/></param>
        public int WriteSinglePoint(double[] buf)
        {
            //SigWrite(_channels, buf);
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 单通道时，更新一个点。直接更新，不经过缓冲区。
        /// </summary>
        /// <param name="buf">用户缓冲区数组, 数组大小须不小于<see cref="NumOfChns"/></param>
        public int WriteSinglePoint(double buf)
        {
            //SigWrite(_channels, buf);
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 等待当前任务完成
        /// </summary>
        /// <param name="timeout">
        /// 等待的时间(单位:ms)
        /// <para>若设置为-1，则无限期等待；</para>
        /// <para>若设置为0，则立即返回检查结果。</para>
        /// </param>
        public int WaitUntilDone(int timeout)
        {
            return _waitUntilDoneEvent.Wait(timeout)?JYErrorCode.NoError:JYErrorCode.TimeOut;
        }

        /// <summary>
        /// 启动数采任务
        /// </summary>
        /// <remarks> 根据配置启动任务</remarks>
        public int Start()
        {
            int ret = 0;
            _isUnderflow = false;
            if(Mode == EnumAOMode.ContinuousWrapping &&
                     ((_localBuffer.Count / _channels.Count) % _devHandle.AODBFBlockSize) == 0)
            {
                _bufLenInSamples = (_localBuffer.Count / _channels.Count);
            }
            else if (_bufLenInSamples == 0)
            {
                _bufLenInSamples = (int)(UpdateRate * 10);
            }
            else if(_bufLenInSamples < 512) //缓冲区不能小于BlockSize
            {
                return JYErrorCode.UserBufferError; 
            }

            //To Add: 配置硬件的AO参数，将本地缓冲区中的数据写入硬件缓冲区，启动AO输出
            //如果是连续NoWrapping模式则还需要启动线程不断从本地缓冲区取出数据写入到硬件缓冲区

            return ret;
        }

        /// <summary>
        /// 停止数采任务
        /// </summary>
        /// <returns>
        /// <para>   0: 成功</para>
        /// <para>-507：内存不足；</para>
        /// <para>  -1：超时，任务未结束</para>
        /// <para>-508: API调用次序不符合要求，或者参数异常</para>
        /// </returns>
        /// <remarks>停止正在执行的任务</remarks>
        public int Stop()
        {
            _aoStarted = false;

            int ret = JYErrorCode.NoError;
            if(Mode != EnumAOMode.Single)
            {
                //StopContAO
            }

            _taskDone = true;

            //单点模式直接退出，不用等线程结束
            if (Mode == EnumAOMode.Single)
            {
                return ret;
            }

            try
            {
                //连续模式需要Stop线程
                //if (false == _thdWriteData.Join(200))
                //{
                //    _thdWriteData.Abort();                    
                //}
            }
            catch { }
            if (_localBuffer != null)
            {
                _localBuffer.Clear();
            }

            return JYErrorCode.NoError;
        }

        ~JYPXIE9529AOTask()
        {
            Stop();
        }

        public int DSA_ConfigSpeedRate()
        {
            int err = JYPXIE9529Import.DSA_ConfigSpeedRate(_devHandle.CardID, 0, 0, 54000, out _adjustedSampleRate);
            return err;
        }
        #endregion

        #region -------------私有方法定义-------------
        //此段定义私有方法
        /// <summary>
        /// 将本地缓冲区的数据写入到硬件
        /// </summary>
        private void ThdWriteData()
        {
            while (TaskDone == false)
            {
                //To Add: 以下添加从本地缓冲区取数据写入硬件缓冲区的代码，同时需要修改_sampleWritten的值

                ActivateWaitEvents();
                Thread.Sleep(1);
            }
            JYLog.Print("Task Done!");
        }

        /// <summary>
        /// 激活等待事件
        /// </summary>
        private void ActivateWaitEvents()
        {
            WaitEvent waitEvent;
            for (int i = 0; i < EventQueue.Count; i++)
            {
                waitEvent = EventQueue.Dequeue();
                if (!waitEvent.IsEnabled) continue; //Just Dequeue when no one is waiting

                if (TaskDone || waitEvent.ConditionHandler())
                    waitEvent.Set();
                else
                    EventQueue.Enqueue(waitEvent);
            }
        }
        #endregion

        #region ----------------AOTask需要用到的结构和枚举的定义---------------
        /// <summary>
        /// AO通道参数类
        /// </summary>
        public sealed class AOChnParam
        {
            /// <summary>
            /// 通道号。与AO通道的物理序号相对应。
            /// </summary>
            public int ChnID { get; private set; }


            private double _rangeLow;
            /// <summary>
            /// 通道量程下限
            /// </summary>
            public double RangeLow // Ignored if gain is not programmable
            {
                get { return _rangeLow; }
                set
                {
                    _rangeLow = value;
                }
            }

            private double _rangeHi;
            /// <summary>
            /// 通道量程上限
            /// </summary>
            public double RangeHi //Ignored if gain is not programmable
            {
                get { return _rangeHi; }
                set
                {
                    _rangeHi = value;
                }
            }

            private EnumAOTerminalConfig _terminalConfig;
            /// <summary>
            /// 输入端口配置方式
            /// </summary>
            public EnumAOTerminalConfig TerminalConfig
            {
                get { return _terminalConfig; }
                set
                {
                    _terminalConfig = value;
                }
            }

            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="chnID">通道物理序号</param>
            /// <param name="chnName">通道名称</param>
            /// <param name="rangeLow">通道量程下限</param>
            /// <param name="rangeHi">通道量程上限</param>
            /// <param name="coupling">耦合方式</param>
            /// <param name="unit">单位</param>
            public AOChnParam(Int32 chnID, double rangeLow, double rangeHi, EnumAOTerminalConfig terminalCfg)
            {
                ChnID = chnID;
                _rangeLow = rangeLow;
                _rangeHi = rangeHi;
                _terminalConfig = terminalCfg;
            }
        }

        /// <summary>
        /// 信号沿类型
        /// </summary>
        public enum EnumAOClkEdge
        {
            /// <summary>
            /// 上升沿
            /// </summary>
            Rising,

            /// <summary>
            /// 下降沿
            /// </summary>
            Falling,
        };

        /// <summary>
        /// 触发沿类型
        /// </summary>
        public enum EnumAOTrgEdge
        {
            /// <summary>
            /// 上升沿
            /// </summary>
            Rising,

            /// <summary>
            /// 下降沿
            /// </summary>
            Falling,
        };

        /// <summary>
        /// 触发信号源
        /// </summary>
        public enum EnumAOTriggerSrc
        {
            /// <summary>
            /// 外部数字触发
            /// </summary>
            ExtDigital
        };

        /// <summary>
        /// 状态锁
        /// </summary>
        internal class StatusLock
        {
            private bool _marked = false;
            /// <summary>
            /// 状态标记, 默认为未使用
            /// </summary>
            public bool Marked
            {
                get { return _marked; }
            }

            /// <summary>
            /// 标记为使用中
            /// </summary>
            public void Mark()
            {
                lock (this)
                {
                    _marked = true;
                }
            }

            /// <summary>
            /// 标记为未使用
            /// </summary>
            public void UnMark()
            {
                lock (this)
                {
                    _marked = false;
                }
            }
        }

        /// <summary>
        /// AO工作模式枚举类型
        /// </summary>
        public enum EnumAOMode
        {
            /// <summary>
            /// 单点方式
            /// </summary>
            Single,

            /// <summary>
            /// 有限点方式
            /// </summary>
            Finite,

            /// <summary>
            /// 连续方式
            /// </summary>
            ContinuousNoWrapping,
            ContinuousWrapping
        };

        /// <summary>
        /// 输入配置枚举类型
        /// </summary>
        public enum EnumAOTerminalConfig
        {
            /// <summary>
            /// 默认配置方式
            /// </summary>
            Default,

            /// <summary>
            /// 参考单端模式
            /// </summary>
            RSE,

            /// <summary>
            /// 非参考单端模式
            /// </summary>
            NRSE,

            /// <summary>
            /// 差分模式
            /// </summary>
            Differential,

            /// <summary>
            /// 伪差分模式
            /// </summary>
            Pseudodifferential
        };


        /// <summary>
        /// 时钟源类型
        /// </summary>
        public enum EnumAOClkSrc
        {
            /// <summary>
            /// 内部时钟源
            /// </summary>
            Internal,

            /// <summary>
            /// 外部时钟源
            /// </summary>
            External
        };

        /// <summary>
        /// AI触发类型，需要根据板卡的实际支持情况修改
        /// </summary>
        public enum EnumAOTriggerType
        {
            /// <summary>
            /// 无触发
            /// </summary>
            Immediate,

            /// <summary>
            /// 软件触发
            /// </summary>
            Software,

            /// <summary>
            /// 直接触发
            /// </summary>
            PostTrigger,
        };

        public class CAOTriggerParam
        {
            /// <summary>
            /// 触发类型，包括：Immediate/Software/DigitalEdge/AnalogEdge
            /// </summary>
            public EnumAOTriggerType TriggerType;

            /// <summary>
            /// 数字触发设置
            /// </summary>
            public CAODigitalTriggerSetting DigitialTriggerSettings;

            /// <summary>
            /// 模拟触发设置
            /// </summary>
            public CAOAnalogTriggerSetting AnalogTriggerSettings;

            /// <summary>
            /// 重复触发设置,为0时不重复触发，>0时为重复触发次数；
            /// </summary>
            public int ReTriggerCount;

            /// <summary>
            /// 触发延迟时间设置，为0时不延迟，>0时为延迟ms数；
            /// </summary>
            public double TriggerDelay;
        }

        public class CAODigitalTriggerSetting
        {
            /// <summary>
            /// 触发源
            /// </summary>
            public EnumAOTriggerSrc TriggerSrc;

            /// <summary>
            /// 数字触发边沿类型，Rising/Falling
            /// </summary>
            public EnumAOTrgEdge TriggerEdge;
        }


        public class CAOAnalogTriggerSetting
        {
            public EnumAOTriggerSrc TriggerSrc; //触发源，根据PXIe-9529的定义包括EXTD/PXIE_STARTIN/PXI_STARTIN/PXI_BUS0 ~7
            public EnumAOTrgEdge TriggerEdge; //数字触发边沿类型，Rising/Falling
            public double TriggerLevel;          //触发门限
        }
        #endregion

        /// <summary>
        /// 清除所有通道
        /// </summary>
        /// <returns></returns>
        public int ClearChannels()
        {
            _channels.Clear();
            return JYErrorCode.NoError;
        }
    }
}
