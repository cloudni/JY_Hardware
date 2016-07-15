using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JYCommon;

namespace JYPXIE69529
{
    /// <summary>
    /// 定义DSA专用的函数等
    /// </summary>
    internal class JYPXIE69529Device
    {
        #region --------------只读字段---------------

        private readonly ushort cardType = JYPXIE69529Import.PXI_9529;
        /// <summary>
        /// 卡的类型，在实例化类对象时根据Device model初始化为对应的类型
        /// </summary>
        public ushort CardType
        {
            get { return cardType; }
        }

        private readonly double _boardClkRate = 4e7; //40MHz
        /// <summary>
        /// 板载时钟
        /// </summary>
        public double BoardClkRate
        {
            get { return _boardClkRate; }
        }

        private readonly uint _diffChannelCnt = 32;
        /// <summary>
        /// 差分或伪差分通道数
        /// </summary>
        public uint DiffChannelCount
        {
            get { return _diffChannelCnt; }
        }

        private readonly uint _seChannelCnt = 64;    //单端通道数
        /// <summary>
        /// 单端或伪单端通道数
        /// </summary>
        public uint SEChannelCount
        {
            get { return _seChannelCnt; }
        }

        private readonly double _maxSampleRateSingleChannel = 500000;
        /// <summary>
        /// 单通道最大采样率
        /// </summary>
        public double MaxSampleRateSingleChannel
        {
            get { return _maxSampleRateSingleChannel; }
        }

        private readonly bool _isAISync = false;
        /// <summary>
        /// 是否是同步采集卡
        /// </summary>
        public bool IsAISync
        {
            get { return _isAISync; }
        }


        private readonly uint _aoChannelCnt = 2;
        /// <summary>
        /// AO通道数
        /// </summary>   
        public uint AOChannelCount
        {
            get { return _aoChannelCnt; }
        }

        private readonly double _maxUpdateRateSingleChannel = 1e6;
        /// <summary>
        /// 单通道最大更新率
        /// </summary>
        public double MaxUpdateRateSingleChannel
        {
            get { return _maxUpdateRateSingleChannel; }
        }

        private readonly bool _isAOSync = false;
        /// <summary>
        /// AO输出是否是同步
        /// </summary>
        public bool IsAOSync
        {
            get { return _isAOSync; }
        }

        /// <summary>
        /// DIO Port 数量
        /// </summary>
        private readonly uint _dioPortCnt = 3;
        public uint DIOPortCnt
        {
            get { return _dioPortCnt; }
        }

        /// <summary>
        /// DIO PortPerLines 
        /// </summary>
        private readonly uint _dioPortPerLines = 8;
        public uint DIOPortPerLines
        {
            get { return _dioPortPerLines; }
        }
        #endregion

        #region ------保存实例化后的一些参数定义-------
        /// <summary>
        /// 板卡编号，构造此类对象时的入参
        /// </summary>
        private ushort _cardnumber;

        /// <summary>
        /// 用于保存每个cardnumber构造出的实例
        /// </summary>
        private static List<JYPXIE69529Device> _listThisInst = null;

        /// <summary>
        /// 调用Register后得到的cardID
        /// </summary>
        private short _cardID;
        public ushort CardID
        {
            get { return (ushort)_cardID; }
        }

        #endregion

        #region -------------构造实例,初始化及释放----------------
        /// <summary>
        /// 根据board number获取操作实例,保证每张板卡只有一个注册实例
        /// </summary>
        /// <param name="cardNum"></param>
        /// <returns></returns>
        public static JYPXIE69529Device GetInstance(ushort cardNum)
        {
            if(_listThisInst == null || !_listThisInst.Exists(t=>t._cardnumber == cardNum))
            {
                JYPXIE69529Device inst = null;
                try
                {
                    inst = new JYPXIE69529Device(cardNum);
                }
                catch(Exception ex)
                {
                    JYLog.Print("硬件初始化失败，Msg={0}", ex.Message);
                    return null;
                }
                if (_listThisInst == null)
                {
                    _listThisInst = new List<JYPXIE69529Device>();
                }
                _listThisInst.Add(inst);
                return inst;
            }
            else
            {
                JYLog.Print("硬件已经初始化，直接返回实例!");
                return _listThisInst.Find(t => t._cardnumber == cardNum);
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        private JYPXIE69529Device(ushort cardNum)
        {
            _cardnumber = cardNum;
            _cardID = -1;

            if (Initiate() == false)
            {
                throw new Exception("初始化失败，可能是板卡编号错误！");
            }
            
        }

        private bool Initiate()
        {
            short cid = -1;

            //在驱动中注册板卡
            cid = JYPXIE69529Import.DSA_Register_Card(cardType, _cardnumber);
            if (cid < 0)  //小于0，说明注册失败，则抛出异常
            {
                return false;
            }
            else
            {
                _cardID = cid;
            }
            return true;
        }
        
        /// <summary>
        /// 关闭AD设备,禁止传输,并释放资源，该函数自动在类的析构函数中执行
        /// </summary>
        private int Release()
        {
            //以下添加释放硬件资源的代码
            int err = 0;

            err = JYPXIE69529Import.DSA_Release_Card((ushort)_cardID);

            return 0;
        }

        /// <summary>
        /// 析构函数，释放硬件资源
        /// </summary>
        ~JYPXIE69529Device()
        {
            Release();
        }
        #endregion

        #region --------------AI相关字段-----------------

        private readonly uint _aiDBFBlockSize = 512;
        /// <summary>
        /// AI double buffer缓冲区的blocksize
        /// </summary>
        public uint AIDBFBlockSize
        {
            get { return _aiDBFBlockSize; }
        }

        private readonly uint _AINDBFBlockSize = 256;
        /// <summary>
        /// AI non double buffer缓冲区的blocksize
        /// </summary>
        public uint AINDBFBlockSize
        {
            get { return _AINDBFBlockSize; }
        }

        private bool _aiReserved;
        /// <summary>
        /// AI是否已经占用的标志
        /// </summary>
        public bool AIReserved
        {
            get
            {
                return _aiReserved;
            }
            set
            {
                _aiReserved = value;
            }
        }

        #endregion
    }
}
