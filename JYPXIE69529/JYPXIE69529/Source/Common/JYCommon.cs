using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace JYCommon
{
    /// <summary>
    /// 等待事件类
    /// </summary>
    internal class WaitEvent
    {
        private AutoResetEvent _autoEvent;
        /// <summary>
        /// AutoResetEvent事件对象
        /// </summary>
        public AutoResetEvent Event
        {
            get { return _autoEvent; }
        }

        private Func<bool> _conditionHandler;
        /// <summary>
        /// 执行此操作，返回值为true时，发出（Set）事件（Event）；否则不发出事件。
        /// </summary>
        public Func<bool> ConditionHandler
        {
            get { return _conditionHandler; }
        }

        private bool _isEnabled;
        /// <summary>
        /// 事件是否处于被等待状态
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
        }

        /// <summary>
        /// 创建等待事件对象
        /// </summary>
        /// <param name="conditionHandler">事件触发条件</param>
        public WaitEvent(Func<bool> conditionHandler)
        {
            _autoEvent = new AutoResetEvent(false);
            _conditionHandler = conditionHandler;
            _isEnabled = false;
        }

        /// <summary>
        /// <para>加入事件队列，并等待一段时间，判断事件是否触发。</para>
        /// <para>若检测到ConditionHandler()或者timeout为0，立即返回，不使用Event.</para>
        /// </summary>
        /// <param name="evQueue">事件队列</param>
        /// <param name="timeout">超时时间(单位:ms)</param>
        /// <returns>
        /// <para>true---触发条件满足</para>
        /// <para>false---触发条件不满足</para>
        /// </returns>
        public bool EnqueueWait(Queue<WaitEvent> evQueue, int timeout)
        {
            if (ConditionHandler())
                return true;
            else if (timeout == 0)
                return false;
            else
            {
                bool stat;
                lock (this)
                {
                    _isEnabled = true;
                    if (evQueue != null)
                        evQueue.Enqueue(this);
                    stat = Event.WaitOne(timeout);
                    _isEnabled = false;
                }
                return stat;
            }
        }

        /// <summary>
        /// 等待一段时间，判断事件是否发出。（不加入事件队列）
        /// 若检测到ConditionHandler()或者timeout为0，立即返回，不使用Event.
        /// </summary>
        /// <param name="timeout">超时时间(单位:ms)</param>
        /// <returns>
        /// <para>true---触发条件满足</para>
        /// <para>false---触发条件不满足</para>
        /// </returns>
        public bool Wait(int timeout)
        {
            return EnqueueWait(null, timeout);
        }

        /// <summary>
        /// 触发事件
        /// </summary>
        /// <returns></returns>
        public void Set()
        {
            /******
             * 将判断ConditionHandler()是否满足的部分放在调用者处, 便于添加额外的条件, 如TaskDone.
             ******/
            if (_isEnabled) //Set only when somebody is waiting
                Event.Set();
        }
    }

    /// <summary>
    /// 日志打印类，用于在程序逻辑中打印一些日记记录到文件，方便调试程序，
    /// 同时客户使用时如果遇到问题，也可以使能日志打印的功能，方便定位错误的原因
    /// </summary>
    public static class JYLog
    {
        /// <summary>
        /// 用于日志消息缓存的队列，首次调用时初始化
        /// </summary>
        private static Queue _logMsgQ;

        /// <summary>
        /// 轮询日志队列的定时器 
        /// </summary>
        private static Timer _timerWriteLog;

        private static bool _enableLog;
        /// <summary>
        /// 使能日志打印功能
        /// </summary>
        public static bool EnableLog
        {
            set
            {
                _enableLog = value;
            }
            get
            {
                return _enableLog;
            }
        }

        static JYLog()
        {
            _enableLog = false;               //初始化为不打印日志            
            Queue q = new Queue(1024);        //初始化大小为1024
            _logMsgQ = Queue.Synchronized(q); //获取Queue.Synchronized方法包装的Queue

            _timerWriteLog = new Timer(FuncWriteLog, null, 0, 1); //创建轮询日志队列的定时器

        }

        /// <summary>
        /// 写入日志文件
        /// </summary>
        /// <param name="logMsg">要打印的消息内容</param>
        /// <param name="args">参数</param>
        /// <returns>0成功，-1失败</returns>
        public static int Print(string logMsg, params object[] args)
        {
            if (_enableLog == false)
            {
                return 1;
            }
            DateTime t = DateTime.Now;
            try
            {
                string callFile = new System.Diagnostics.StackTrace(true).GetFrame(1).GetFileName();
                int callLine = new System.Diagnostics.StackTrace(true).GetFrame(1).GetFileLineNumber();

                string msg = string.Format(logMsg, args); //2016-04-26 10:59:10.6679687
                string s_t = string.Format("[{0:D4}-{1:D2}-{2:D2} {3}][{4}][{5}]\t",
                                           t.Year, t.Month, t.Day, t.TimeOfDay.ToString(), callFile, callLine);

                _logMsgQ.Enqueue(s_t + msg + "\r\n");
            }
            catch
            {
                return -1;
            }

            return 0;
        }

        private static bool _writting = false;
        /// <summary>
        /// 轮询日志队列的定时器回调函数
        /// </summary>
        /// <param name="state"></param>
        private static void FuncWriteLog(object state)
        {
            //如果之前的回调正在进行或日志队列为空，都直接返回
            if (_writting == true || _logMsgQ.Count <= 0)
            {
                return;
            }
            _writting = true;

            try
            {
                DateTime t = DateTime.Now;
                //指定日志文件的目录
                string fname = Directory.GetCurrentDirectory() + "\\"
                               + t.Year.ToString("D04") + t.Month.ToString("D02") + t.Day.ToString("D02") + ".log";

                //定义文件信息对象
                FileInfo finfo = new FileInfo(fname);
                FileStream fs = null;
                if (!finfo.Exists)
                {
                    fs = File.Create(fname);
                    fs.Close();
                    finfo = new FileInfo(fname);
                }

                //判断文件是否存在以及是否大于10M
                if (finfo.Length > 1024 * 1024 * 10)
                {
                    //文件超过10MB则重命名
                    File.Move(Directory.GetCurrentDirectory() + "\\" + t.Year.ToString("D04") + t.Month.ToString("D02") + t.Day.ToString("D02") + ".log",
                              Directory.GetCurrentDirectory() + "\\" + t.Year.ToString("D04") + t.Month.ToString("D02") + t.Day.ToString("D02") + t.Hour.ToString("D02") + t.Minute.ToString("D02") + t.Second.ToString("D02") + ".log");
                }

                fs = finfo.OpenWrite();
                StreamWriter w = new StreamWriter(fs);

                while (_logMsgQ.Count > 0)
                {
                    //设置写数据流的起始位置为文件流的末尾
                    w.BaseStream.Seek(0, SeekOrigin.End);

                    //写入日志内容并换行
                    w.Write(_logMsgQ.Dequeue().ToString());
                }
                //清空缓冲区内容，并把缓冲区内容写入基础流
                w.Flush();

                //关闭写数据流
                w.Close();
                fs.Close();


            }
            catch { }

            _writting = false;
        }
    }

    /// <summary>
    /// 错误代码的定义
    /// </summary>
    internal static class JYErrorCode
    {
        /// <summary>
        /// 无错误
        /// </summary>
        public static int NoError = 0;

        /// <summary>
        /// 超时
        /// </summary>
        public static int TimeOut = -10001;

        /// <summary>
        /// 参数错误
        /// </summary>
        public static int ErrorParam = -10002;

        /// <summary>
        /// 调用顺序不正确
        /// </summary>
        public static int IncorrectCallOrder = -10003;

        /// <summary>
        /// 当前配置不能调用该方法
        /// </summary>
        public static int CannotCall = -10004;

        /// <summary>
        /// 用户缓冲区错误
        /// </summary>
        public static int UserBufferError = -10005;

        /// <summary>
        /// 缓冲区溢出
        /// </summary>
        public static int BufferOverflow = -10006;

        /// <summary>
        /// 缓冲区下溢出
        /// </summary>
        public static int BufferDownflow = -10007;

        public static int RangeParamInvalid = -10008;

        public static int ChannelIDInvalid = -10009;

        public static int DIOPortIsReserved = -100010;

        public static int ArrayLengthNotMatch = -100011;

    }

    /// <summary>
    /// 循环缓冲队列类
    /// </summary>
    /// <typeparam name="T">泛型</typeparam>
    internal class CircularBuffer<T>
    {
        private readonly int _sizeOfT; //T的Size，创建队列的时候初始化
        private T[] _buffer;           //缓冲区

        private int _WRIdx;           //队列写指针
        private int _RDIdx;           //队列读指针

        private volatile int _numOfElement;
        /// <summary>
        /// 当前的元素个数
        /// </summary>
        public int NumOfElement
        {

            get
            {
                lock (this)
                {
                    return _numOfElement;
                }
            }
        }

        private int _bufferSize;       //循环队列缓冲的大小 
        /// <summary>
        /// 缓冲区的大小
        /// </summary>
        public int BufferSize
        {
            get { return _bufferSize; }
        }

        /// <summary>
        /// 当前能容纳的点数
        /// </summary>
        public int CurrentCapacity
        {
            get
            {
                lock (this)
                {
                    return _bufferSize - _numOfElement;
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bufferSize"></param>
        public CircularBuffer(int bufferSize)
        {
            if (bufferSize <= 0) //输入的size无效，创建默认大小的缓冲区
            {
                bufferSize = 1024;
            }
            _bufferSize = bufferSize;

            _buffer = new T[_bufferSize]; //新建对应大小的缓冲区

            _WRIdx = 0;
            _RDIdx = 0;    //初始化读写指针

            _numOfElement = 0;

            _sizeOfT = Marshal.SizeOf(_buffer[0]);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CircularBuffer()
        {
            _bufferSize = 1024;

            _buffer = new T[_bufferSize]; //新建对应大小的缓冲区

            _WRIdx = 0;
            _RDIdx = 0;    //初始化读写指针

            _numOfElement = 0;

            _sizeOfT = Marshal.SizeOf(_buffer[0]);
        }

        /// <summary>
        /// 调整缓冲区大小，数据会被清空
        /// </summary>
        /// <param name="size"></param>
        public void AdjustSize(int size)
        {
            lock (this)
            {
                if (size <= 0)
                {
                    size = 1; //最小size应当为1
                }
                this.Clear();
                _bufferSize = size;
                _buffer = new T[_bufferSize];
            }
        }

        /// <summary>
        /// 清空缓冲区内的数据
        /// </summary>
        public void Clear()
        {
            lock (this)
            {
                _numOfElement = 0;
                _WRIdx = 0;
                _RDIdx = 0;
            }
        }

        /// <summary>
        /// 向缓冲队列中放入一个数据
        /// </summary>
        /// <param name="element"></param>
        public int Enqueue(T element)
        {
            lock (this)
            {
                if (_numOfElement >= _bufferSize)
                {
                    return -1;
                }
                _buffer[_WRIdx] = element;

                if (_WRIdx + 1 >= _bufferSize)
                {
                    _WRIdx = 0;
                }
                else
                {
                    _WRIdx++;
                }

                _numOfElement++;
                return 1;
            }
        }

        /// <summary>
        /// 向缓冲队列中放入一组数据
        /// </summary>
        /// <param name="elements"></param>
        public int Enqueue(T[] elements)
        {
            lock (this)
            {
                if (_numOfElement + elements.Length > _bufferSize)
                {
                    return -1;
                }

                //超出数组尾部了，应该分两次拷贝进去，先拷贝_WRIdx到结尾的，再从头开始拷贝
                if (_WRIdx + elements.Length > _bufferSize)
                {
                    Buffer.BlockCopy(elements, 0, _buffer, _WRIdx * _sizeOfT, (_bufferSize - _WRIdx) * _sizeOfT);
                    int PutCnt = _bufferSize - _WRIdx;
                    int remainCnt = elements.Length - PutCnt;
                    _WRIdx = 0;
                    Buffer.BlockCopy(elements, PutCnt * _sizeOfT, _buffer, _WRIdx * _sizeOfT, remainCnt * _sizeOfT);
                    _WRIdx = remainCnt;
                }
                else
                {
                    Buffer.BlockCopy(elements, 0, _buffer, _WRIdx * _sizeOfT, elements.Length * _sizeOfT);
                    if (_WRIdx + elements.Length == _bufferSize)
                    {
                        _WRIdx = 0;
                    }
                    else
                    {
                        _WRIdx += elements.Length;
                    }
                }
                _numOfElement += elements.Length;

                return elements.Length;
            }
        }

        /// <summary>
        /// 从缓冲队列中取一个数据
        /// </summary>
        /// <returns>失败：-1，1：返回一个数据</returns>
        public int Dequeue(ref T reqElem)
        {
            lock (this)
            {
                if (_numOfElement <= 0)
                {
                    return -1;
                }
                _numOfElement--;

                reqElem = _buffer[_RDIdx];

                if (_RDIdx + 1 >= _bufferSize)
                {
                    _RDIdx = 0;
                }
                else
                {
                    _RDIdx++;
                }

                return 1;
            }
        }

        /// <summary>
        /// 从缓冲队列中取出指定长度的数据
        /// </summary>
        /// <param name="reqBuffer">请求读取缓冲区</param>
        /// <returns>返回实际取到的数据长度</returns>
        public int Dequeue(ref T[] reqBuffer, int len)
        {
            lock (this)
            {
                int getCnt = len;

                if (len > _numOfElement || _numOfElement <= 0)
                {
                    return -1;
                }
                else if (len <= 0)
                {
                    getCnt = _numOfElement;
                }

                if (_RDIdx + getCnt > _bufferSize)   //取数据的总大小超过了应该分两次拷贝，先拷贝尾部，剩余的从头开始拷贝
                {
                    Buffer.BlockCopy(_buffer, _RDIdx * _sizeOfT, reqBuffer, 0, (_bufferSize - _RDIdx) * _sizeOfT);
                    int fetchedCnt = (_bufferSize - _RDIdx);
                    int remainCnt = getCnt - fetchedCnt;
                    _RDIdx = 0;
                    Buffer.BlockCopy(_buffer, _RDIdx * _sizeOfT, reqBuffer, fetchedCnt * _sizeOfT, remainCnt * _sizeOfT);
                    _RDIdx = remainCnt;
                }
                else
                {
                    Buffer.BlockCopy(_buffer, _RDIdx * _sizeOfT, reqBuffer, 0, getCnt * _sizeOfT);
                    if (_RDIdx + getCnt == _bufferSize)
                    {
                        _RDIdx = 0;
                    }
                    else
                    {
                        _RDIdx += getCnt;
                    }
                }
                _numOfElement -= getCnt;
                return getCnt;
            }
        }

        /// <summary>
        /// 从缓冲队列中取出指定长度的数据
        /// </summary>
        /// <param name="reqBuffer">请求读取缓冲区</param>
        /// <returns>返回实际取到的数据长度</returns>
        public int Dequeue(ref T[,] reqBuffer, int len)
        {
            lock (this)
            {
                int getCnt = len;

                if (len > _numOfElement || _numOfElement <= 0)
                {
                    return -1;
                }
                else if (len <= 0)
                {
                    getCnt = _numOfElement;
                }

                if (_RDIdx + getCnt > _bufferSize)   //取数据的总大小超过了应该分两次拷贝，先拷贝尾部，剩余的从头开始拷贝
                {
                    Buffer.BlockCopy(_buffer, _RDIdx * _sizeOfT, reqBuffer, 0, (_bufferSize - _RDIdx) * _sizeOfT);
                    int fetchedCnt = (_bufferSize - _RDIdx);
                    int remainCnt = getCnt - fetchedCnt;
                    _RDIdx = 0;
                    Buffer.BlockCopy(_buffer, _RDIdx * _sizeOfT, reqBuffer, fetchedCnt * _sizeOfT, remainCnt * _sizeOfT);
                    _RDIdx = remainCnt;
                }
                else
                {
                    Buffer.BlockCopy(_buffer, _RDIdx * _sizeOfT, reqBuffer, 0, getCnt * _sizeOfT);
                    if (_RDIdx + getCnt == _bufferSize)
                    {
                        _RDIdx = 0;
                    }
                    else
                    {
                        _RDIdx += getCnt;
                    }
                }
                _numOfElement -= getCnt;
                return getCnt;
            }
        }
    }
}
