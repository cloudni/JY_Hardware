using System;
using System.Collections.Generic;
using System.Threading;
using JYCommon;
using System.Linq;
using System.Runtime.InteropServices;

namespace JYPXIE9529
{
    /// <summary>
    /// AI采集任务类,是Sealed类，不可被继承
    /// </summary>
    public sealed class JYPXIE9529AITask
    {
        /// <summary>
        /// 构造函数,仅输入板卡编号
        /// </summary>
        /// <param name="boardNum">板卡的编号</param>
        public JYPXIE9529AITask(int boardNum)
        {
            //获取板卡操作类的实例
            _devHandle = JYPXIE9529Device.GetInstance((ushort)boardNum);
            if (_devHandle == null)
            {
                throw new Exception("初始化失败，请检查board number或硬件连接！");
            }
            _adjustedSampleRate = 1000;//_operator.GetRealAcqRate(sampleRate); //根据需要的采样率获取真实的采样率
            _samplesToAcquire = 1000; //每通道采样点数
            _acqMode = EnumAIMode.Finite; //采样模式

            _channels = new List<AIChnParam>();

            _clkSrc = EnumAIClkSrc.Internal;
            _clkEdge = EnumAIClkEdge.Rising;
            TriggerParam = new CAITriggerParam();
            TriggerParam.TriggerType = EnumAITriggerType.Immediate;

            _bufLenInSamples = (int)(_adjustedSampleRate * 20); //默认缓冲20s钟

            _localBuffer = new CircularBuffer<short>(); //本地软件缓冲区设置            

            _samplesFetchedPerChannel = 0;

            _waitUntilDoneEvent = new WaitEvent(() => _taskDone);
            _eventQueue = new Queue<WaitEvent>(8);

            _AI_EnableIEPE = false;
        }

        #region -------------------私有字段-------------------------
        //添加需要使用的私有属性字段
        private bool _AI_EnableIEPE;
        private static int AI_SAMPLE_COUNT = 65536;
        private static int AI_CHANNEL_COUNT = 8;
        private static int AI_SAMPLE_RATE = 54000; //54Khz

        private int AI_ReadCount = JYPXIE9529AITask.AI_SAMPLE_COUNT;
	    private IntPtr RDBuffer0;
        private IntPtr RDBuffer1;
        private ushort BufferId0;
        private ushort BufferId1;

        /// <summary>
        /// 操作硬件的对象
        /// </summary>
        private JYPXIE9529Device _devHandle;

        /// <summary>
        /// AI是否已启动
        /// </summary>
        private bool _aiStarted;

        /// <summary>
        /// 本地缓冲内存
        /// </summary>
        private CircularBuffer<short> _localBuffer;

        private Thread _thdAcquireData;

        /// <summary>
        /// 通道列表
        /// </summary>
        private ushort[] _channelArray;

        /// <summary>
        /// 量程列表
        /// </summary>
        private ushort[] _rangeArray;

        /// <summary>
        /// 单点采集是的数组
        /// </summary>
        private short[] _channelValueArray;

        /// <summary>
        /// BufferID号
        /// </summary>
        private ushort _aiBufferID;

        /// <summary>
        /// AI硬件双缓冲区的大小
        /// </summary>
        private uint _AIDoubleBuffSize;

        /// <summary>
        /// AI读缓冲区1（对齐地址）
        /// </summary>
        private IntPtr _AIReadbuffer_alignment1;

        /// <summary>
        /// AI读缓冲区2（对齐地址）
        /// </summary>
        private IntPtr _AIReadbuffer_alignment2;

        /// <summary>
        /// AI读缓冲区（非对齐地址）
        /// </summary>
        private IntPtr _AIReadbuffer;

        /// <summary>
        /// AI是否使能了DoubleBuffer模式
        /// </summary>
        private bool _enableAIDbfMode;

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
                    _aiStarted = false;
                }
                else if (Mode != EnumAIMode.Single)
                {
                    _aiStarted = true;
                }

            }

        }

        /// <summary>
        /// WaitUntilDone等待事件
        /// </summary>
        private WaitEvent _waitUntilDoneEvent;

        private int _samplesFetchedPerChannel;

        /// <summary>
        /// 等待锁, 用于限制多线程并行读操作. 需要等一个线程读取完成后, 另一个线程才能读(排队).
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

        private bool _isOverflow;

        private int _samplesPerChannel = 1000;

        public int SamplesPerChannel
        {
            get { return _samplesPerChannel; }
            set
            {
                _samplesPerChannel = value;
            }
        }
        #endregion

        #region --------------------公共属性定义----------------------
        private readonly List<AIChnParam> _channels;
        /// <summary>
        /// 通道列表
        /// </summary>
        public List<AIChnParam> Channels
        {
            get { return _channels; }
        }

        private EnumAIMode _acqMode;
        /// <summary>
        /// 采集模式，支持Single/Finite/Continuous三种类型
        /// </summary>
        public EnumAIMode Mode
        {
            get { return _acqMode; }
            set
            {
                _acqMode = value;
            }
        }

        private double _adjustedSampleRate;
        /// <summary>
        /// 每通道采样率
        /// </summary>
        public double SampleRate
        {
            get { return _adjustedSampleRate; }
            set
            {
                //先根据value的值计算出分频倍数，然后在写入到_adjustedSampleRate
                _adjustedSampleRate = value;//_devHandle.GetRealAcqRate(value); ; //此处需要反算出来后保存
                _bufLenInSamples = (int)(_adjustedSampleRate * 20);
            }
        }

        private int _samplesToAcquire;
        /// <summary>
        /// 有限点采集时, 每通道采集的样点数。若设置为小于0，则采集无穷个点。
        /// <para>默认值为256</para>
        /// </summary>
        public int SamplesToAcquire
        {
            get { return _samplesToAcquire; }
            set
            {
                _samplesToAcquire = value;
            }
        }

        private int _bufLenInSamples;
        /// <summary>
        /// 缓冲区能容纳的每通道样点数。一次读取的样点数不能超过此容量。        
        /// <remarks>在调用 Start() 方法后分配或者调整缓冲区。</remarks>
        public int BufLenInSamples
        {
            get { return _bufLenInSamples; }
            set
            {
                Interlocked.Exchange(ref _bufLenInSamples, value);
            }
        }

        private int _availableSamples;
        /// <summary>
        /// 缓冲区内可以读取的点数
        /// </summary>
        public int AvailableSamples
        {
            get
            {
                return _availableSamples;
            }
            private set
            {
                Interlocked.Exchange(ref _availableSamples, value);
            }
        }

        private EnumTimeBaseSrc _timeBaseSrc;
        /// <summary>
        /// 时钟源,需要根据不同厂商驱动的规定去修改EnumClkSrc枚举
        /// </summary>
        public EnumTimeBaseSrc TimeBaseSrc
        {
            get { return _timeBaseSrc; }
            set
            {
                _timeBaseSrc = value;
            }
        }

        private EnumAIClkSrc _clkSrc;
        /// <summary>
        /// 时钟源,需要根据不同厂商驱动的规定去修改EnumClkSrc枚举
        /// </summary>
        public EnumAIClkSrc ClkSrc
        {
            get { return _clkSrc; }
            set
            {
                _clkSrc = value;
            }
        }

        private EnumAIClkEdge _clkEdge;
        /// <summary>
        /// 时钟沿。仅在外部时钟时有效。
        /// </summary>
        public EnumAIClkEdge ClkEdge
        {
            get { return _clkEdge; }
            set
            {
                _clkEdge = value;
            }
        }

        private CAITriggerParam _triggerParam;
        /// <summary>
        /// AI触发参数设置
        /// </summary>
        public CAITriggerParam TriggerParam
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

        #region --------------------公共方法定义--------------------

        /// <summary>
        /// 添加通道
        /// </summary>
        /// <param name="chnID">通道物理序号</param>
        /// <param name="chnName">通道名称</param>
        /// <param name="rangeLow">通道量程下限</param>
        /// <param name="rangeHi">通道量程上限</param>
        /// <param name="coupling">耦合方式</param>
        /// <param name="terminalCfg">端口输入模式配置</param>
        /// <returns></returns>
        public int AddChannel(int chnID, double rangeLow, double rangeHi, EnumCoupling coupling, EnumAITerminalConfig terminalCfg)
        {
            //To Add 添加通道的代码  
            int err = 0;

            /// 判断量程范围
            if (rangeHi <= rangeLow)
            {
                return JYErrorCode.RangeParamInvalid;
            }

            ///判断通道ID
            if (chnID < -1)
            {
                return JYErrorCode.ChannelIDInvalid;
            }
            err = CheckChannelID(chnID, terminalCfg);
            if (err != JYErrorCode.NoError)
            {
                return err;
            }
            ///添加通道到列表
            ///如果为-1，则根据接线端方式添加所有通道
            if (chnID == -1)
            {
                int channelLength = 0;
                switch (terminalCfg)
                {
                    case EnumAITerminalConfig.Default:
                    case EnumAITerminalConfig.NRSE:
                    case EnumAITerminalConfig.RSE:
                        channelLength = (int)_devHandle.SEChannelCount;
                        break;
                    case EnumAITerminalConfig.Differential:
                        channelLength = (int)_devHandle.DiffChannelCount;
                        break;
                    default:
                        channelLength = (int)_devHandle.SEChannelCount;
                        break;

                }

                for (int i = 0; i < channelLength; i++)
                {
                    _channels.Add(new AIChnParam(i, rangeLow, rangeHi, coupling, terminalCfg));
                }
            }
            _channels.Add(new AIChnParam(chnID, rangeLow, rangeHi, coupling, terminalCfg));
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 添加通道
        /// </summary>
        /// <param name="chnID">要添加通道的所有物理序号</param>
        /// <param name="chnName">通道名称</param>
        /// <param name="rangeLow">通道量程下限</param>
        /// <param name="rangeHi">通道量程上限</param>
        /// <param name="coupling">耦合方式</param>
        /// <param name="terminalCfg">端口输入模式配置</param>
        /// <returns></returns>
        public int AddChannel(int[] chnsID, double rangeLow, double rangeHi, EnumCoupling coupling, EnumAITerminalConfig terminalCfg)
        {
            //To Add 添加通道的代码  

            int err = 0;

            /// 判断量程范围
            if (rangeHi <= rangeLow)
            {
                return JYErrorCode.RangeParamInvalid;
            }


            foreach (var item in chnsID)
            {
                ///判断通道ID
                if (item < 0)
                {
                    return JYErrorCode.ChannelIDInvalid;
                }
                err = CheckChannelID(item, terminalCfg);
                if (err != JYErrorCode.NoError)
                {
                    return err;
                }
            }

            foreach (var item in chnsID)
            {
                _channels.Add(new AIChnParam(item, rangeLow, rangeHi, coupling, terminalCfg));

            }
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 删除指定通道号的通道,为-1则删除所有通道
        /// </summary>
        /// <param name="chnID">要删除的通道的通道号</param>
        public int RemoveChannel(int chnID)
        {
            //To Add 添加通道的代码 

            int idx = _channels.FindIndex(t => t.ChnID == chnID);
            if (idx >= 0)
            {
                _channels.RemoveAt(idx);
            }

            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 读取数据，按列返回采集到的电压值
        /// </summary>
        /// <param name="buffer">用户缓冲区数组</param>
        /// <param name="bufLen">用户缓冲区能容纳的每通道样点数</param>
        /// <param name="timeout">当数据不足时，最多等待的时间（单位：ms）</param>
        /// <param name="readLen">每通道实际获取的样点数</param>
        /// <param name="Deterministic">是否返回确定性结果</param>
        /// <param name="timeStamp">读回的第一个样点对应的时间戳</param>        
        /// <remarks>
        /// <list type="bullet">
        /// <item>若缓冲区内可读取数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若任务已结束，则直接读取缓冲区内的剩余数据；否则，参考下一条。</item>
        /// <item>等待数据，在timeout时间内，若数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若等待timeout时间后，缓冲区数据量仍未达到SamplesPerChannel，则抛出超时异常；抛出异常前，依据Deterministic的值，若为false，则读取缓冲区内的所有数据，否则不读取数据。</item>
        /// </list>
        /// </remarks>
        /// <returns>
        /// 小于0：实际错误代码
        /// 大于0：实际读到的每通道点数
        /// </returns>
        public int ReadData(ref double[,] Buf, int SamplesPerChannel, bool Deterministic, int timeout)
        {
            int ret = 0;
            short[,] TmpBuf = new short[SamplesPerChannel, _channels.Count];
            ret = ReadRawData(ref TmpBuf, SamplesPerChannel, Deterministic, timeout);

            if (ret == 0)
            {
                return ScaleRawData(TmpBuf, Buf);
            }
            return ret;
        }

        /// <summary>
        /// 读取数据，按列返回采集到的电压值
        /// </summary>
        /// <param name="Buf">用户缓冲区数组</param>
        /// <param name="SamplesPerChannel">用户缓冲区能容纳的每通道样点数</param>
        /// <param name="Deterministic">是否返回确定性结果</param>
        /// <param name="timeout">当数据不足时，最多等待的时间（单位：ms）</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item>若缓冲区内可读取数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若任务已结束，则直接读取缓冲区内的剩余数据；否则，参考下一条。</item>
        /// <item>等待数据，在timeout时间内，若数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若等待timeout时间后，缓冲区数据量仍未达到SamplesPerChannel，则抛出超时异常；抛出异常前，依据Deterministic的值，若为false，则读取缓冲区内的所有数据，否则不读取数据。</item>
        /// </list>        
        /// </remarks>
        /// <returns>
        /// 小于0：实际错误代码
        /// 大于0：实际读到的每通道点数
        /// </returns>
        public int ReadData(ref double[] Buf, int SamplesPerChannel, bool Deterministic, int timeout)
        {
            int ret = 0;
            short[] TmpBuf = new short[SamplesPerChannel * _channels.Count];
            ret = ReadRawData(ref TmpBuf, SamplesPerChannel, Deterministic, timeout);

            if (ret == 0)
            {
                return ScaleRawData(TmpBuf, Buf);

            }
            return ret;
        }

        /// <summary>
        /// 读取数据，按列返回采集到的电压值
        /// </summary>
        /// <param name="Buf">用户缓冲区数组</param>
        /// <param name="SamplesPerChannel">用户缓冲区能容纳的每通道样点数</param>
        /// <param name="Deterministic">是否返回确定性结果</param>
        /// <param name="timeout">超时时间</param>  
        /// <remarks>
        /// <list type="bullet">
        /// <item>若缓冲区内可读取数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若任务已结束，则直接读取缓冲区内的剩余数据；否则，参考下一条。</item>
        /// <item>等待数据，在timeout时间内，若数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若等待timeout时间后，缓冲区数据量仍未达到SamplesPerChannel，则抛出超时异常；抛出异常前，依据Deterministic的值，若为false，则读取缓冲区内的所有数据，否则不读取数据。</item>
        /// </list>
        public int ReadRawData(ref short[,] Buf, int SamplesPerChannel, bool Deterministic, int timeout)
        {
            if (Mode == EnumAIMode.Single)
            {
                return JYErrorCode.CannotCall;
            }
            else if (_taskDone && SamplesPerChannel > AvailableSamples)
            {
                return JYErrorCode.IncorrectCallOrder;
            }

            if (SamplesPerChannel <= 0)
            {
                return JYErrorCode.ErrorParam; //数组长度不够
            }
            if ((Buf.GetLength(1) < _channels.Count || Buf.GetLength(0) < SamplesPerChannel))
            {
                return JYErrorCode.UserBufferError;
            }

            bool isTimeout = false;
            int retSamples = 0;
            lock (_waitLock) //防止多个线程同时读取；要求“排队”读取。
            {
                //To Add: 等待缓冲区内的数据足够之后进行读取
                // Handle Wait & TaskDone
                WaitEvent waitEvent = new WaitEvent(() => TaskDone || (AvailableSamples >= SamplesPerChannel));
                if (!waitEvent.EnqueueWait(EventQueue, timeout))
                {
                    isTimeout = true;
                }
                int availableSamples = AvailableSamples;
                if (isTimeout == false && availableSamples >= SamplesPerChannel)
                {
                    retSamples = SamplesPerChannel;
                }
                else if (isTimeout == true && Deterministic == false)
                {
                    retSamples = availableSamples;
                }
                else if (isTimeout == true && Deterministic == true)
                {
                    JYLog.Print("读取超时，需要返回确定性结果！");
                    return JYErrorCode.TimeOut;
                }
                _localBuffer.Dequeue(ref Buf, retSamples * _channels.Count);

            }

            //缓冲区队列溢出
            if (_isOverflow)
            {
                _isOverflow = false;
                return JYErrorCode.BufferOverflow;
            }
            if (isTimeout)
            {
                return JYErrorCode.TimeOut;
            }
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 读取数据，按列返回采集到的电压值
        /// </summary>
        /// <param name="Buf">用户缓冲区数组</param>
        /// <param name="SamplesPerChannel">用户缓冲区能容纳的每通道样点数</param>
        /// <param name="Deterministic">是否返回确定性结果</param>
        /// <param name="timeout">超时时间</param>  
        /// <remarks>
        /// <list type="bullet">
        /// <item>若缓冲区内可读取数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若任务已结束，则直接读取缓冲区内的剩余数据；否则，参考下一条。</item>
        /// <item>等待数据，在timeout时间内，若数据量达到SamplesPerChannel，则直接读取数据；否则，参考下一条。</item>
        /// <item>若等待timeout时间后，缓冲区数据量仍未达到SamplesPerChannel，则抛出超时异常；抛出异常前，依据Deterministic的值，若为false，则读取缓冲区内的所有数据，否则不读取数据。</item>
        /// </list>
        /// <returns>
        /// 小于0：实际错误代码
        /// 大于0：实际读到的每通道点数
        /// </returns> 
        public int ReadRawData(ref short[] Buf, int SamplesPerChannel, bool Deterministic, int timeout)
        {
            var buf = new short[SamplesPerChannel, _channels.Count];
            int ret = ReadRawData(ref buf, SamplesPerChannel, Deterministic, timeout);
            if (ret > 0)
            {
                Buffer.BlockCopy(buf, 0, Buf, 0, ret * _channels.Count * sizeof(short));
            }

            return ret;
        }


        /// <summary>
        /// 每通道读取一个样点，非缓冲式读取。返回应用变换系数变换后的数据。
        /// </summary>
        /// <param name="buffer">用户缓冲区数组。数组大小须不小于<see cref="NumOfChns"/>。</param>
        /// <returns>
        /// </returns>
        public int ReadSinglePoint(ref double[] buffer)
        {
            int err = 0;
            if (_aiStarted == true && Mode == EnumAIMode.Single)
            {
                for (int i = 0; i < _channels.Count; i++)
                {
                    if ((err = JYPXIE9529Import.DSA_AI_ContVScale(_devHandle.CardID, _rangeArray[i], _channelValueArray[i], out buffer[i])) < 0)
                    {
                        return err;
                    }
                    JYLog.Print("Voltage Scale {0}", buffer[i]);
                }

                return err;
            }
            else
            {
                if (Mode == EnumAIMode.Single)
                {
                    return JYErrorCode.IncorrectCallOrder;
                }
                else
                {
                    return JYErrorCode.CannotCall;
                }
            }
        }

        /// <summary>
        /// 单通道读取一个样点，非缓冲式读取。返回应用变换系数变换后的数据。
        /// </summary>
        /// <param name="buffer">用户缓冲区数组。数组大小须不小于<see cref="NumOfChns"/>。</param>
        /// <returns>
        /// </returns>
        public int ReadSinglePoint(ref double buffer)
        {
            int err = 0;
            if (_aiStarted == true && Mode == EnumAIMode.Single)
            {
                var aiBuffer = new double[_channels.Count];
                err = ReadSinglePoint(ref aiBuffer);
                if (err < JYErrorCode.NoError)
                {
                    return err;
                }

                buffer = aiBuffer[0];

                return _devHandle.ReadSinglePoint(ref buffer);
            }
            else
            {
                if (Mode == EnumAIMode.Single)
                {
                    return JYErrorCode.IncorrectCallOrder;
                }
                else
                {
                    return JYErrorCode.CannotCall;
                }
            }
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
            if (_taskDone == true)
            {
                return JYErrorCode.NoError;
            }
            return _waitUntilDoneEvent.EnqueueWait(_eventQueue, timeout) ? JYErrorCode.NoError : JYErrorCode.TimeOut;
        }

        /// <summary>
        /// 启动数采任务
        /// </summary>
        /// <returns>
        /// <para>   0: 成功</para>
        /// <para>-507：内存不足；</para>
        /// <para>  -1：超时，任务未结束</para>
        /// <para>-508: API调用次序不符合要求，或者参数异常</para>
        /// </returns>
        /// <remarks> 根据配置启动任务</remarks>
        public int Start()
        {
            /*Enable Double Buffer Mode*/
            if (JYPXIE9529Import.DSA_AI_AsyncDblBufferMode(_devHandle.CardID, true) != 0)
            {
                throw new Exception("AI_AsyncDblBufferMode Error");
            }

            /*Setup Buffer for AI DMA Transfer*/
            if (JYPXIE9529Import.DSA_AI_ContBufferSetup(_devHandle.CardID, RDBuffer0, (uint)AI_ReadCount, out BufferId0) != 0)
            {
                throw new Exception("AI_ContBufferSetup0 Error");
            }

            if (JYPXIE9529Import.DSA_AI_ContBufferSetup(_devHandle.CardID, RDBuffer1, (uint)AI_ReadCount, out BufferId1) != 0)
            {
                throw new Exception("AI_ContBufferSetup1 Error");
            }

            if (JYPXIE9529Import.DSA_AI_ContReadChannel(_devHandle.CardID, (ushort)Channels[0].ChnID, (ushort)0, (uint)BufferId0, (uint)AI_ReadCount, (double)0, JYPXIE9529Import.ASYNCH_OP) != 0)
            {
                throw new Exception("DSA_AI_ContReadChannel");
            }


            int ret = JYErrorCode.NoError;
            _samplesFetchedPerChannel = 0;
            _isOverflow = false;

            _bufLenInSamples = (int)(SampleRate * 20);

            _channelArray = new ushort[_channels.Count];
            _rangeArray = new ushort[_channels.Count];
            _channelValueArray = new short[_channels.Count];

            //To Add: 添加Start硬件采集的代码，如果是单点则只需要标记AI的启动和占用
            //如果是连续采集则需要启动后台线程，不断从硬件缓冲区中读取数据

            if ((ret = AIConfig()) < 0)
            {
                return ret;
            }

            if (_acqMode == EnumAIMode.Single)
            {
                _aiStarted = true;
                _devHandle.AIReserved = true;
                return JYErrorCode.NoError;
            }
            else
            {
                //配置连续或有限点采样的缓冲区
                if ((ret = ConfigContAcq()) == JYErrorCode.NoError)
                {
                    _localBuffer = new CircularBuffer<short>(_bufLenInSamples * _channels.Count);
                    _thdAcquireData = new Thread(ThdAcquireData);

                    _devHandle.AIReserved = true;
                    ret = StartContAI();
                    if (ret == 0)
                    {
                        JYLog.Print("AI Started OK!");
                        TaskDone = false;
                        _thdAcquireData.Start();
                    }
                    else
                    {
                        _devHandle.AIReserved = false;
                    }

                }
                return ret;

            }
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
            _aiStarted = false;

            int ret = JYErrorCode.NoError;
            if (_devHandle != null)
            {
                //StopContAI
            }

            if (_taskDone == true)
            {
                return ret;
            }
            _taskDone = true;

            //单点模式直接退出，不用等线程结束
            if (Mode == EnumAIMode.Single)
            {
                return ret;
            }
            try
            {
                //连续采集需要停止从硬件取数据的线程
                //if (true == _thdAcquireData.Join(200))
                //{
                //    return ret;
                //}
                //else
                //{
                //    _thdAcquireData.Abort();
                //    return JYErrorCode.NoError;
                //}
            }
            catch { }
            return JYErrorCode.NoError;
        }

        ~JYPXIE9529AITask()
        {
            Stop();
        }

        public int DSA_ConfigSpeedRate()
        {
            int err = JYPXIE9529Import.DSA_ConfigSpeedRate(_devHandle.CardID, 0, 0, 54000, out _adjustedSampleRate);
            return err;
        }

        public int DSA_AI_ConfigChannel(ushort channelID)
        {
            return JYPXIE9529Import.DSA_AI_9529_ConfigChannel(_devHandle.CardID, channelID, true, JYPXIE9529Import.AD_B_10_V, JYPXIE9529Import.P9529_AI_Diff | JYPXIE9529Import.P9529_AI_Coupling_DC);
        }

        public short DSA_TRG_Config()
        {
            return JYPXIE9529Import.DSA_TRG_Config(_devHandle.CardID, JYPXIE9529Import.P9529_TRG_AI, JYPXIE9529Import.P9529_TRG_SRC_NOWAIT, 0, 0);
        }

        #endregion

        #region -------------私有方法定义-------------
        //此段定义私有方法
        /// <summary>
        /// 从缓冲区取数据的线程函数
        /// </summary>
        private void ThdAcquireData()
        {
            JYLog.Print("AI Task Started...");
            short[] buffer = null;
            if (_enableAIDbfMode == true)
            {
                buffer = new short[_AIDoubleBuffSize * _channels.Count / 2];
            }
            else
            {
                buffer = new short[_AIDoubleBuffSize * _channels.Count];
            }
            int readCnt = 0;
            while (TaskDone == false)
            {
                //To Add: 以下添加从缓冲区取数据到本地缓冲区的代码，同时需要修改_samplesFetchedPerChannel的值
                if ((readCnt = FetchBuffer(ref buffer)) > 0)
                {
                    _samplesFetchedPerChannel += readCnt / _channels.Count;
                    EnQueueElems(buffer);
                    if (_acqMode == EnumAIMode.Finite)
                    {
                        if (_samplesFetchedPerChannel >= _samplesToAcquire)
                        {
                            JYLog.Print("Finite Task Done!");
                            TaskDone = true;
                        }
                    }
                }
                ActivateWaitEvents(); //激活等待事件
                Thread.Sleep(1);
            }
            JYLog.Print("Fetch data Thread Exit!");
        }

        /// <summary>
        /// 数据放入队列尾部
        /// </summary>
        /// <param name="buffer"></param>
        private void EnQueueElems(short[] buffer)
        {
            if (_localBuffer.NumOfElement + buffer.Length > _bufLenInSamples * _channels.Count)
            {
                _isOverflow = true;
                return;
            }
            _localBuffer.Enqueue(buffer);
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

        /// <summary>
        /// 判断通道号是否有效
        /// </summary>
        /// <param name="channelID"></param>
        /// <param name="terminalChannel"></param>
        /// <returns></returns>
        private int CheckChannelID(int channelID, EnumAITerminalConfig terminalChannel)
        {

            if ((channelID >= _devHandle.DiffChannelCount) && (terminalChannel == EnumAITerminalConfig.Differential))
            {
                return JYErrorCode.ChannelIDInvalid;
            }
            else if ((channelID >= _devHandle.SEChannelCount) && (terminalChannel != EnumAITerminalConfig.Differential))
            {
                return JYErrorCode.ChannelIDInvalid;
            }

            return JYErrorCode.NoError;

        }

        /// <summary>
        /// 将RawData转换为Scale的数值
        /// </summary>
        /// <param name="rawbuf"></param>
        /// <param name="scaledData"></param>
        /// <returns></returns>
        public int ScaleRawData(short[,] rawbuf, double[,] scaledData)
        {
            short err = 0;
            int rowLength = 0;
            int columnLength = 0;

            rowLength = rawbuf.GetLength(0);
            columnLength = rawbuf.GetLength(1);
            //rawbuf中的数据按列存储

            for (int i = 0; i < columnLength; i++)
            {
                for (int j = 0; j < rowLength; j++)
                {
                    scaledData = null;
                    if ((err = JYPXIE9529Import.DSA_AI_ContVScale(_devHandle.CardID, _rangeArray[i], rawbuf[i, j], out scaledData[i, j])) < 0)
                    {
                        scaledData = null;
                        return err;
                    }
                }

            }

            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 将RawData转换为Scale的数值
        /// </summary>
        /// <param name="rawbuf"></param>
        /// <param name="scaledData"></param>
        /// <returns></returns>
        public int ScaleRawData(short[] rawbuf, double[] scaledData)
        {
            short err = 0;

            int samplePerChannel = 0;

            samplePerChannel = rawbuf.Length / _channels.Count;

            //rawbuf中的数据交织存储

            for (int i = 0; i < samplePerChannel; i++)
            {
                for (int j = 0; j < _channels.Count; j++)
                {
                    scaledData = null;
                    if ((err = JYPXIE9529Import.DSA_AI_ContVScale((ushort)_devHandle.CardID, _rangeArray[j], rawbuf[i * _channels.Count + j], out scaledData[i * _channels.Count + j])) < 0)
                    {
                        scaledData = null;
                        return err;
                    }
                }

            }

            return JYErrorCode.NoError;
        }


        /// <summary>
        /// 获取里requestsize最近的Blocksize的整数倍的数
        /// </summary>
        /// <param name="requestSize"></param>
        /// <param name="blockSize"></param>
        /// <returns></returns>
        private uint GetNearestOfMBlocksize(uint requestSize, uint blockSize)
        {
            if (blockSize <= 0)
            {
                return requestSize;
            }
            uint ITmp = requestSize;
            while (true)
            {
                if (ITmp % blockSize == 0)
                {
                    break;
                }
                else
                {
                    ITmp++;
                }
            }
            return ITmp;
        }

        /// <summary>
        /// 配置AI采样速率，通道模式和触发
        /// <param name="rate">配置的采样率</param>
        /// <param name="actualRate">返回的实际采样率</param>
        /// <param name="ClkSrc">采样时钟源</param>
        /// <param name="trigType">触发类型</param>
        /// <param name="trigSrc">触发源</param>
        /// <param name="edge">触发沿类型</param>
        /// <param name="triggerLevel">模拟触发电平</param>
        /// <param name="retriggerCnt">重触发次数</param>
        /// <param name="delay_us">触发延迟时间</param>
        /// <returns></returns>
        public int AIConfigClockAndTrigger()
        {
            short err = 0;
            ushort wConfigCtrl = 0;
            uint wTrigCtrl = 0;
            uint midOrDelayCnt = 0;
            ushort mCnt = 0;
            ushort anaTrigCtrl = 0;
            ushort hTrigLevel = 0;
            ushort lTrigLevel = 0;
            ushort _retrigCnt = 0;
            ushort timeBaseSrc = 0;

            if (_acqMode != EnumAIMode.Single)
            {
                /// 配置时钟
                switch (_clkSrc)
                {
                    case EnumAIClkSrc.Internal:
                        wConfigCtrl = JYPXIE9529Import.DAQ2K_AI_ADCONVSRC_Int;
                        break;
                    case EnumAIClkSrc.SSI:
                        wConfigCtrl = JYPXIE9529Import.DAQ2K_AI_ADCONVSRC_SSI;
                        break;
                    case EnumAIClkSrc.AFI0:
                        wConfigCtrl = JYPXIE9529Import.DAQ2K_AI_ADCONVSRC_AFI0;
                        break;
                    default:
                        wConfigCtrl = JYPXIE9529Import.DAQ2K_AI_ADCONVSRC_Int;
                        break;
                }

                switch (_timeBaseSrc)
                {
                    case EnumTimeBaseSrc.IntTimeBase:
                        timeBaseSrc = JYPXIE9529Import.DAQ2K_IntTimeBase;
                        break;
                    case EnumTimeBaseSrc.ExtTimeBase:
                        timeBaseSrc = JYPXIE9529Import.DAQ2K_ExtTimeBase;
                        break;
                    case EnumTimeBaseSrc.SSITimeBase:
                        timeBaseSrc = JYPXIE9529Import.DAQ2K_SSITimeBase;
                        break;
                    default:
                        timeBaseSrc = JYPXIE9529Import.DAQ2K_IntTimeBase;
                        break;
                }

                /// 配置触发
                if (_triggerParam.TriggerType != EnumAITriggerType.Immediate)
                {
                    switch (_triggerParam.TriggerType)
                    {
                        case EnumAITriggerType.DelayTrigger:
                            wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGMOD_DELAY;
                            break;
                        case EnumAITriggerType.MidTrigger:
                            wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGMOD_MIDL;
                            break;
                        case EnumAITriggerType.PostTrigger:
                            wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGMOD_POST;
                            break;
                        case EnumAITriggerType.PreTrigger:
                            wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGMOD_PRE;
                            break;
                        default:
                            return JYErrorCode.ErrorParam; //参数错误

                    }
                    if (_triggerParam.AnalogTriggerSettings != null)
                    {
                        switch (_triggerParam.AnalogTriggerSettings.TriggerSrc)
                        {
                            case EnumAIAnalogTriggerSrc.ChannelAnalog:
                                anaTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGSRC_ANA;
                                break;
                            case EnumAIAnalogTriggerSrc.ExtAnalog:
                                anaTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGSRC_ANA;
                                break;
                            default:
                                return JYErrorCode.ErrorParam;//参数错误
                        }

                        switch (_triggerParam.AnalogTriggerSettings.TriggerEdge)
                        {
                            case EnumAIAnalogTrgEdge.AboveHighLevle:
                                anaTrigCtrl |= JYPXIE9529Import.Above_High_Level;
                                break;
                            case EnumAIAnalogTrgEdge.BelowLowLevel:
                                anaTrigCtrl |= JYPXIE9529Import.Below_Low_level;
                                break;
                            case EnumAIAnalogTrgEdge.InsideRegion:
                                anaTrigCtrl |= JYPXIE9529Import.Inside_Region;
                                break;
                            case EnumAIAnalogTrgEdge.HighHysteresis:
                                anaTrigCtrl |= JYPXIE9529Import.High_Hysteresis;
                                break;
                            case EnumAIAnalogTrgEdge.LowHysteresis:
                                anaTrigCtrl |= JYPXIE9529Import.Low_Hysteresis;
                                break;
                            default:
                                return JYErrorCode.ErrorParam;//参数错误
                        }

                        hTrigLevel = (ushort)(0xFF * (_triggerParam.AnalogTriggerSettings.TriggerHighLevel / 20) - 0x80);
                        lTrigLevel = (ushort)(0xFF * (_triggerParam.AnalogTriggerSettings.TriggerLowLevel / 20) - 0x80);
                    }

                    if (_triggerParam.DigitialTriggerSettings != null)
                    {
                        switch (_triggerParam.DigitialTriggerSettings.TriggerSrc)
                        {
                            case EnumAIDigitalTriggerSrc.ExtDigital:
                                wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRGSRC_ExtD;
                                break;
                            case EnumAIDigitalTriggerSrc.SSI:
                                wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TRSRC_SSI;
                                break;
                            default:
                                return JYErrorCode.ErrorParam;//参数错误
                        }

                        switch (_triggerParam.DigitialTriggerSettings.TriggerEdge)
                        {
                            case EnumAIDigitalTrgEdge.Falling:
                                wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TrgNegative;
                                break;
                            case EnumAIDigitalTrgEdge.Rising:
                                wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_TrgPositive;
                                break;
                            default:
                                return JYErrorCode.ErrorParam;//参数错误
                        }

                    }

                    if (_triggerParam.PreTriggerCnt > 0)
                    {
                        if (_triggerParam.TriggerType == EnumAITriggerType.DelayTrigger || _triggerParam.TriggerType == EnumAITriggerType.PostTrigger)
                        {
                            wTrigCtrl |= JYPXIE9529Import.DAQ2K_AI_ReTrigEn;
                            _retrigCnt = (ushort)_triggerParam.PreTriggerCnt;
                        }
                    }

                    if (_triggerParam.TriggerType == EnumAITriggerType.DelayTrigger)
                    {
                        midOrDelayCnt = (uint)_triggerParam.TriggerDelay;
                    }
                    else if (_triggerParam.TriggerType == EnumAITriggerType.MidTrigger)
                    {
                        midOrDelayCnt = (ushort)_triggerParam.MidTriggerAffterCnt;
                        mCnt = (ushort)_triggerParam.MidTriggerBeforeCnt;
                    }
                    else if (_triggerParam.TriggerType == EnumAITriggerType.PreTrigger)
                    {
                        mCnt = (ushort)_triggerParam.PreTriggerCnt;
                    }

                }

            }
            if ((err = JYPXIE9529Import.DSA_AIO_Config(_devHandle.CardID, timeBaseSrc, anaTrigCtrl, hTrigLevel, lTrigLevel)) < 0)
            {
                return err;
            }

            if ((err = JYPXIE9529Import.DSA_AI_Config(_devHandle.CardID, wConfigCtrl, wTrigCtrl, midOrDelayCnt, mCnt, _retrigCnt, true)) < 0)
            {
                return err;
            }
            return 0;
        }

        /// <summary>
        /// 连续采集配置
        /// </summary>
        /// <returns></returns>
        private int ConfigContAcq()
        {
            short adlinkErr;
            _enableAIDbfMode = false;

            if (_acqMode == EnumAIMode.Single)
            {
                return JYErrorCode.CannotCall;
            }
            //缓冲区的大小,双缓冲时，为每一个缓冲区的大小（大小都是所有通道大小的总和）
            else if (_acqMode == EnumAIMode.Finite)
            {
                if (_triggerParam.ReTriggerCount == 0) //不需要重触发
                {
                    //纠正双缓冲的大小为通道数的偶数倍
                    uint initalSize = (uint)(_adjustedSampleRate / 50); //20ms 
                    initalSize = initalSize == 0 ? 1 : initalSize;
                    var initBuffersize = GetNearestOfMBlocksize(initalSize, _devHandle.AIDBFBlockSize);

                    if (_samplesToAcquire < initBuffersize * 8) //如果有限点的点数较小，则不使用double buffer模式
                    {
                        //Finite模式下,,每通道取的点数必须是块的整数倍,如果用户配置的不是，则向上调整到最近的
                        _AIDoubleBuffSize = GetNearestOfMBlocksize((uint)_samplesToAcquire, _devHandle.AIDBFBlockSize);

                    }
                    else
                    {
                        _enableAIDbfMode = true; //使用双缓冲模式
                    }
                }
                else //需要重触发，则直接使用
                {
                    //Finite模式下,每通道取的点数必须是块的整数倍,如果用户配置的不是，则向上调整到最近的
                    _AIDoubleBuffSize = GetNearestOfMBlocksize((uint)_samplesToAcquire, _devHandle.AIDBFBlockSize);
                    _enableAIDbfMode = true; //使用双缓冲模式
                }
            }
            else
            {
                //纠正双缓冲的大小为通道数的偶数倍
                uint initalSize = (uint)(_adjustedSampleRate / 50); //20ms 
                initalSize = initalSize == 0 ? 1 : initalSize;
                _AIDoubleBuffSize = GetNearestOfMBlocksize(initalSize, _devHandle.AIDBFBlockSize);
                _enableAIDbfMode = true; //使用双缓冲模式
            }

            //设置Double Buffer Mode
            adlinkErr = JYPXIE9529Import.DSA_AI_AsyncDblBufferMode(_devHandle.CardID, _enableAIDbfMode);
            if (adlinkErr < 0)
            {
                JYLog.Print("配置AI双缓冲失败！errorcode={0}", adlinkErr);
                return adlinkErr;
            }
            int alignment_byte = 16;
            _AIReadbuffer = Marshal.AllocHGlobal((int)_AIDoubleBuffSize * _channels.Count * sizeof(short) + alignment_byte);
            _AIReadbuffer_alignment1 = new IntPtr(alignment_byte * (((long)_AIReadbuffer + (alignment_byte - 1)) / alignment_byte));
            if (_enableAIDbfMode == true)
            {
                _AIReadbuffer_alignment2 = new IntPtr(alignment_byte * (((long)_AIReadbuffer + (alignment_byte - 1)) / alignment_byte));
            }
            return JYErrorCode.NoError;
        }

        /// <summary>
        /// 从本地缓冲区中取采集的数据
        /// </summary>
        /// <param name="retbuffer">取到的数据，多个通道是interleaved的</param>
        /// <returns>
        /// 小于0：失败，具体看错误代码
        /// 大于0：成功，值代表每通道返回的样点数
        /// </returns>
        private int FetchBuffer(ref short[] retbuffer)
        {

            if (_aiStarted == false)
            {
                JYLog.Print("Error, AI 未启动！");
                return -1;
            }
            short err = 0;
            bool bStopped, bHalfReady;

            if (_enableAIDbfMode == true)
            {
                err = JYPXIE9529Import.DSA_AI_AsyncDblBufferHalfReady(_devHandle.CardID, out bHalfReady, out bStopped);
                if (bHalfReady == true)
                {

                    if (err != JYPXIE9529Import.NoError)
                    {
                        return err;
                    }
                    else
                    {
                        short[] buffer = new short[_AIDoubleBuffSize * _channels.Count / 2];
                        if (_aiBufferID == 0)
                        {
                            Marshal.Copy(_AIReadbuffer_alignment1, retbuffer, 0, (int)(_AIDoubleBuffSize * _channels.Count / 2));
                        }
                        else
                        {
                            Marshal.Copy(_AIReadbuffer_alignment2, retbuffer, 0, (int)(_AIDoubleBuffSize * _channels.Count / 2));
                        }
                        _aiBufferID = (ushort)((_aiBufferID + 1) % 2);
                        return (int)_AIDoubleBuffSize * _channels.Count / 2;
                    }
                }
            }
            else
            {
                uint dwAccessCnt;
                uint startPos;
                err = JYPXIE9529Import.DSA_AI_AsyncCheck(_devHandle.CardID, out bStopped, out dwAccessCnt);
                if (err != JYPXIE9529Import.NoError)
                {
                    return err;
                }

                if (bStopped == true)
                {
                    //err = JYPXIE9529Import.DSA_AI_AsyncClear(_devHandle.CardID, out startPos, out dwAccessCnt);
                    err = JYPXIE9529Import.DSA_AI_AsyncClear(_devHandle.CardID, out dwAccessCnt);
                    if (err != JYPXIE9529Import.NoError)
                    {
                        return err;
                    }

                    //short[] buffer = new short[_AIDoubleBuffSize * _channels.Count];
                    Marshal.Copy(_AIReadbuffer_alignment1, retbuffer, 0, (int)(_AIDoubleBuffSize * _channels.Count));
                    //retbuffer = (ushort[])((object)buffer);
                    return (int)_AIDoubleBuffSize * _channels.Count;
                }
            }
            return 0;
        }

        /// <summary>
        /// 配置AI读取
        /// </summary>
        private int StartContAI()
        {
            short err = 0;
            uint AIScanInterval = 0;
            uint AISampleInterval = 0;

            AIScanInterval = (uint)(_devHandle.BoardClkRate / SampleRate);
            AISampleInterval = (uint)(AIScanInterval / _channels.Count);

            err |= JYPXIE9529Import.DSA_AI_ContBufferSetup(_devHandle.CardID, _AIReadbuffer_alignment1, (uint)(_AIDoubleBuffSize * _channels.Count), out _aiBufferID);
            if (_enableAIDbfMode == true)
            {
                err |= JYPXIE9529Import.DSA_AI_ContBufferSetup(_devHandle.CardID, _AIReadbuffer_alignment2, (uint)(_AIDoubleBuffSize * _channels.Count), out _aiBufferID);
                err |= JYPXIE9529Import.DSA_AI_ContReadChannel(_devHandle.CardID, 0, (uint)(_AIDoubleBuffSize * _channels.Count), AIScanInterval, AISampleInterval, JYPXIE9529Import.ASYNCH_OP);
            }
            else
                err |= JYPXIE9529Import.DSA_AI_ContReadChannel(_devHandle.CardID, _aiBufferID, (uint)(_AIDoubleBuffSize * _channels.Count), AIScanInterval, AISampleInterval, JYPXIE9529Import.ASYNCH_OP);

            _aiBufferID = 0;

            if (err == JYErrorCode.NoError)
            {
                _aiStarted = true;
                return JYErrorCode.NoError;
            }
            else
            {
                return err;
            }
        }


        /// <summary>
        /// AI相关的配置，调用原厂Config函数进行配置
        /// </summary>
        /// <returns></returns>
        private int AIConfig()
        {
            int err = 0;

            if (_channels.Count <= 0)
            {
                return JYErrorCode.ErrorParam;
            }

            //保存添加的AI通道数
            for (int i = 0; i < _channels.Count; i++)
            {
                //匹配当前通道号是否已经添加到通道列表中，添加了则使能该通道，否则就不使能该通道
                _channelArray[i] = (ushort)_channels[i].ChnID;
                _rangeArray[i] = RangeAndTerminalChange(_channels[i].RangeHi, _channels[i].RangeLow, _channels[i].TerminalConfig);
            }

            err = JYPXIE9529Import.DSA_AI_ContReadChannel(_devHandle.CardID, (ushort)_channels.Count, _channelArray, _rangeArray);

            return err;
        }

        /// <summary>
        /// 转换通道范围
        /// </summary>
        private ushort RangeAndTerminalChange(double highLevel, double lowLevel, EnumAITerminalConfig termianlCfg)
        {
            ushort aiRange = 0;
            ushort terminal = 0;

            if (lowLevel >= -1.25 && highLevel <= 1.25)
            {
                aiRange = JYPXIE9529Import.AD_B_1_25_V;
            }
            else if (lowLevel >= -2.5 && highLevel <= 2.5)
            {
                aiRange = JYPXIE9529Import.AD_B_2_5_V;
            }
            else if (lowLevel >= -5 && highLevel <= 5)
            {
                aiRange = JYPXIE9529Import.AD_B_5_V;
            }
            else
                aiRange = JYPXIE9529Import.AD_B_10_V;

            switch (termianlCfg)
            {
                case EnumAITerminalConfig.Default:
                    terminal = (ushort)JYPXIE9529Import.AI_RSE;
                    break;
                case EnumAITerminalConfig.RSE:
                    terminal = (ushort)JYPXIE9529Import.AI_RSE;
                    break;
                case EnumAITerminalConfig.NRSE:
                    terminal = (ushort)JYPXIE9529Import.AI_NRSE;
                    break;
                case EnumAITerminalConfig.Differential:
                    terminal = (ushort)JYPXIE9529Import.AI_DIFF;
                    break;
                default:
                    terminal = (ushort)JYPXIE9529Import.AI_RSE;
                    break;
            }

            return (ushort)(aiRange | terminal);

        }
        #endregion

        #region ----------------AITask需要用到的结构和枚举的定义---------------
        /// <summary>
        /// 耦合方式枚举类型
        /// </summary>
        public enum EnumCoupling
        {
            /// <summary>
            /// 默认方式
            /// </summary>
            Default,

            /// <summary>
            /// 交流耦合方式
            /// </summary>
            AC,
            /// <summary>
            /// 直流耦合方式
            /// </summary>
            DC
        };

        /// <summary>
        /// AI通道参数类
        /// </summary>
        public sealed class AIChnParam
        {
            /// <summary>
            /// 通道号。与AI通道的物理序号相对应。
            /// </summary>
            public Int32 ChnID { get; private set; }

            private double _rangeLow;
            /// <summary>
            /// 通道量程下限
            /// </summary>
            public double RangeLow
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
            public double RangeHi
            {
                get { return _rangeHi; }
                set
                {
                    _rangeHi = value;
                }
            }

            private EnumCoupling _coupling;
            /// <summary>
            /// 耦合方式
            /// </summary>
            public EnumCoupling Coupling
            {
                get { return _coupling; }
            }

            private EnumAITerminalConfig _terminalConfig;
            /// <summary>
            /// 端口模式配置
            /// </summary>
            public EnumAITerminalConfig TerminalConfig
            {
                get { return _terminalConfig; }
            }

            /// <summary>
            /// 构造函数，创建AIChnParam对象
            /// </summary>
            /// <param name="chnID">通道物理序号</param>
            /// <param name="chnName">通道名称</param>
            /// <param name="rangeLow">通道量程下限</param>
            /// <param name="rangeHi">通道量程上限</param>
            /// <param name="coupling">耦合方式</param>
            public AIChnParam(Int32 chnID, double rangeLow, double rangeHi, EnumCoupling coupling, EnumAITerminalConfig terminalCfg)
            {
                ChnID = chnID;
                _rangeLow = rangeLow;
                _rangeHi = rangeHi;
                _coupling = coupling;
                _terminalConfig = terminalCfg;
            }

        }

        /// <summary>
        /// AI触发类型，需要根据板卡的实际支持情况修改
        /// </summary>
        public enum EnumAITriggerType
        {
            /// <summary>
            /// 无触发
            /// </summary>
            Immediate,

            /// <summary>
            /// 直接触发
            /// </summary>
            PostTrigger,

            /// <summary>
            /// 延迟触发
            /// </summary>
            DelayTrigger,

            /// <summary>
            /// 预触发
            /// </summary>
            PreTrigger,

            /// <summary>
            /// 中间触发
            /// </summary>
            MidTrigger
        };

        /// <summary>
        /// AI时钟沿类型
        /// </summary>
        public enum EnumAIClkEdge
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
        /// AI数字触发沿类型
        /// </summary>
        public enum EnumAIDigitalTrgEdge
        {
            /// <summary>
            /// 上升沿
            /// </summary>
            Rising,

            /// <summary>
            /// 下降沿
            /// </summary>
            Falling
        };

        /// <summary>
        /// AI触发沿类型
        /// </summary>
        public enum EnumAIAnalogTrgEdge
        {
            /// <summary>
            /// 低于低电平
            /// </summary>
            BelowLowLevel,

            /// <summary>
            /// 高于高电平
            /// </summary>
            AboveHighLevle,

            /// <summary>
            /// 在高低电平之间
            /// </summary>
            InsideRegion,

            /// <summary>
            /// 高电平迟滞
            /// </summary>
            HighHysteresis,

            /// <summary>
            /// 低电平迟滞
            /// </summary>
            LowHysteresis
        };

        /// <summary>
        /// 数字触发信号源
        /// </summary>
        public enum EnumAIDigitalTriggerSrc
        {
            /// <summary>
            /// 外部数字触发
            /// </summary>
            ExtDigital,

            /// <summary>
            /// SSI总线数字触发
            /// </summary>
            SSI

        };

        /// <summary>
        /// 模拟触发信号源
        /// </summary>
        public enum EnumAIAnalogTriggerSrc
        {
            /// <summary>
            /// 外部模拟触发
            /// </summary>
            ExtAnalog,

            /// <summary>
            /// 通道模拟触发
            /// </summary>
            ChannelAnalog
        };

        /// <summary>
        /// AI工作模式枚举类型
        /// </summary>
        public enum EnumAIMode
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
            Continuous
        };

        /// <summary>
        /// 输入配置枚举类型
        /// </summary>
        public enum EnumAITerminalConfig
        {
            /// <summary>
            /// 默认配置方式
            /// </summary>
            Default,

            /// <summary>
            /// 参考单端
            /// </summary>
            RSE,

            /// <summary>
            /// 非参考单端模式
            /// </summary>
            NRSE,

            ///// <summary>
            ///// 差分模式
            ///// </summary>
            Differential

            ///// <summary>
            ///// 伪差分模式
            ///// </summary>
            //Pseudodifferential
        };


        /// <summary>
        /// 时钟源类型
        /// </summary>
        public enum EnumAIClkSrc
        {
            /// <summary>
            /// 内部时钟源
            /// </summary>
            Internal,

            /// <summary>
            /// SSI总线时钟源
            /// </summary>
            SSI,

            /// <summary>
            /// AFI-0总线时钟源
            /// </summary>
            AFI0
        };

        /// <summary>
        /// TimeBase源
        /// </summary>
        public enum EnumTimeBaseSrc
        {
            /// <summary>
            /// 内部时基
            /// </summary>
            IntTimeBase,

            /// <summary>
            /// 外部时基
            /// </summary>
            ExtTimeBase,

            /// <summary>
            /// SSI时基
            /// </summary>
            SSITimeBase
        }

        public class CAITriggerParam
        {
            public EnumAITriggerType TriggerType;//触发类型，包括：Immediate/Software/DigitalEdge/AnalogEdge
            public CAIDigitalTriggerSetting DigitialTriggerSettings;
            public CAIAnalogTriggerSetting AnalogTriggerSettings;
            public int ReTriggerCount;  //重复触发设置,为0时不重复触发，>0时为重复触发次数；
            public double TriggerDelay; //触发延迟时间设置，为0时不延迟，>0时为延迟ms数；
            public int PreTriggerCnt;   //预触发采集点数；
            public int MidTriggerBeforeCnt;  //中触发前采样点数；
            public int MidTriggerAffterCnt;  //中触发后采样点数；
        }

        public class CAIDigitalTriggerSetting
        {
            public EnumAIDigitalTriggerSrc TriggerSrc; //触发源
            public EnumAIDigitalTrgEdge TriggerEdge; //数字触发边沿类型，Rising/Falling
        }


        public class CAIAnalogTriggerSetting
        {
            public EnumAIAnalogTriggerSrc TriggerSrc; //触发源，
            public EnumAIAnalogTrgEdge TriggerEdge; //数字触发边沿类型，Rising/Falling
            public double TriggerHighLevel;          //触发高电平门限
            public double TriggerLowLevel;       //触发低电平门限
        }


        #endregion
    }
}
