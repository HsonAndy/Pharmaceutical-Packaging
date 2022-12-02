using System;
using Basic;
using MyUI;
using System.Threading;
using System.Drawing;
using LeadShineUI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace App_CCD檢測機
{
     public partial class Form1 : Form
    {
        MyThread Thread_RS232_communication;
        MyThread Thread_RS485to232_communication;
        MyThread Thread_PLC計算;
        MyTimer myTimer_modbus延遲 = new MyTimer();
        public Form1()     
        {
            InitializeComponent();            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            plC_UI_Init1.Run(this.FindForm(), lowerMachine_Panel1);
            comConterUI1.Init("COM19");
            rS485ModbusUI1.Init("COM1");
            this.timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (plC_UI_Init1.Init_Finish)
            {
                Thread_RS232_communication = new MyThread();
                Thread_RS232_communication.Add_Method(sub_RS232_communication);
                //Thread_RS232_communication.Add_Method(sub_RS485_Temprature);
                Thread_RS232_communication.AutoRun(true);
                Thread_RS232_communication.AutoStop(true);
                Thread_RS232_communication.Trigger();

                Thread_PLC計算 = new MyThread();
                Thread_PLC計算.Add_Method(sub_印字步進電子齒輪比換算);
               // Thread_PLC計算.Add_Method(sub_拉袋DA值線性減少換算);
                Thread_PLC計算.AutoRun(true);
                Thread_PLC計算.AutoStop(true);
                Thread_PLC計算.Trigger();


                Thread_RS485to232_communication = new MyThread();
                Thread_RS485to232_communication.Add_Method(sub_RS485_Temprature);
                Thread_RS485to232_communication.AutoRun(true);
                Thread_RS485to232_communication.AutoStop(true);
                Thread_RS485to232_communication.Trigger();

                this.timer1.Stop();
            }
            
        }

        #region 落藥計數RS232通訊
        int cnt_RS232_communication = 0;
        PLC_Device PLC_Device_RS232_ESP32_medicine_落藥計數收值 = new PLC_Device("S2155");
        PLC_Device PLC_Device_RS232_ESP32_medicine_落藥計數歸零 = new PLC_Device("S2156");
        void sub_RS232_communication()
        {
            if (cnt_RS232_communication == 0)
            {
                cnt_RS232_communication++;
            }
            if (cnt_RS232_communication == 1)
            {
                if (comConterUI1.Flag_IsInitDone)
                {
                    comConterUI1.Command_GetCouunt();
                }
                cnt_RS232_communication = 0;
            }
            if (PLC_Device_RS232_ESP32_medicine_落藥計數歸零.Bool) comConterUI1.Command_ResetCouunt();


        }
        private void plC_Button28_btnClick(object sender, EventArgs e)
        {
            if (comConterUI1.Flag_IsInitDone) comConterUI1.Command_ResetCouunt();
        }

        #endregion
        #region RS485_Modbus通訊
        int cnt_RS485_Temprature = 0;
        PLC_Device PLC_Device_RS485_Temprature_初始化完成 = new PLC_Device("S25000");
        void sub_RS485_Temprature()
        {

            if (cnt_RS485_Temprature == 0)
            {
                cnt_RS485_Temprature++;
            }
            if (cnt_RS485_Temprature == 1)
            {
                if (rS485ModbusUI1.Flag_IsInitDone)
                {
                    PLC_Device_RS485_Temprature_初始化完成.Bool = true;
                    cnt_RS485_Temprature++;
                }               
            }

            if (cnt_RS485_Temprature == 2)
            {
                myTimer_modbus延遲.TickStop();
                myTimer_modbus延遲.StartTickTime(150);
                rS485ModbusUI1.ComSend_GetStation1_PV();
                cnt_RS485_Temprature++;
            }
            if (cnt_RS485_Temprature == 3)
            {
                if (myTimer_modbus延遲.IsTimeOut())
                {
                    myTimer_modbus延遲.TickStop();
                    myTimer_modbus延遲.StartTickTime(150);
                    rS485ModbusUI1.ComSend_GetStation2_PV();
                    cnt_RS485_Temprature++;
                }

            }
            if (cnt_RS485_Temprature == 4)
            {
                if (myTimer_modbus延遲.IsTimeOut())
                {
                    myTimer_modbus延遲.TickStop();
                    myTimer_modbus延遲.StartTickTime(150);
                    rS485ModbusUI1.ComSend_GetStation3_CH01_DA_Value();
                    cnt_RS485_Temprature++;
                }

            }
            if (cnt_RS485_Temprature == 5)
            {
                if (myTimer_modbus延遲.IsTimeOut())
                {
                    myTimer_modbus延遲.TickStop();
                    myTimer_modbus延遲.StartTickTime(150);
                    rS485ModbusUI1.ComSend_SetStation3_CH01_DA_Value();
                    cnt_RS485_Temprature = 0;
                }

            }

            //if (cnt_RS485_Temprature == 6)
            //{
            //    if (myTimer_modbus延遲.IsTimeOut())
            //    {
            //        cnt_RS485_Temprature = 2;
            //    }

            //}



            //rS485ModbusUI1.ComSend_SetStation1_SV();
            //rS485ModbusUI1.ComSend_SetStation2_SV();

        }
        #endregion
        #region 印字步進電子齒輪比換算
        double 印字步進電子齒輪比分子 = 100;
        double 印字步進電子齒輪比分母 = 70;
        double 電子齒輪比;
        double axis0_pos;
        double axis0_cmd;
        double axis0_cmd_buff;
        PLC_Device 印字步進現在位置 = new PLC_Device("D15050");
        PLC_Device 印字步進目標位置設定 = new PLC_Device("D15022");
        PLC_Device 印字步進目標位置換算 = new PLC_Device("D15012");
        PLC_Device 印字步進換算開始 = new PLC_Device("S15068");
        PLC_Device 印字步進換算完成 = new PLC_Device("S15069");
        int cnt_印字步進換算 = 65534;
        void sub_印字步進電子齒輪比換算()
        {
            sub_印字步進電子齒輪比();
        }
        void sub_印字步進電子齒輪比()
        {

            電子齒輪比 = 印字步進電子齒輪比分子 / 印字步進電子齒輪比分母;
            axis0_cmd = dmC1000B1.GetAxisCmdPos(0) / (印字步進電子齒輪比分子 / 印字步進電子齒輪比分母);
            axis0_cmd_buff = axis0_cmd;
            印字步進現在位置.Value = (int)Math.Round(axis0_cmd_buff, 0, MidpointRounding.AwayFromZero) ;
            if (cnt_印字步進換算 == 65534)
            {
                cnt_印字步進換算 = 65535;
            }
            if (cnt_印字步進換算 == 65535) cnt_印字步進換算 = 1;
            if (cnt_印字步進換算 == 1) cnt_Program_印字步進電子齒輪比_檢查按下(ref cnt_印字步進換算);
            if (cnt_印字步進換算 == 2) cnt_Program_印字步進電子齒輪比_開始換算(ref cnt_印字步進換算);
            if (cnt_印字步進換算 == 3) cnt_Program_印字步進電子齒輪比_換算完成(ref cnt_印字步進換算);
            if (cnt_印字步進換算 == 4) cnt_印字步進換算 = 65500;
            if (cnt_印字步進換算 == 65500)
            {
                cnt_印字步進換算 = 65535;
            }
            void cnt_Program_印字步進電子齒輪比_檢查按下(ref int cnt)
            {
                if (印字步進換算開始.Bool) cnt++;
            }
            void cnt_Program_印字步進電子齒輪比_開始換算(ref int cnt)
            {
                axis0_pos = 印字步進目標位置設定.Value * 電子齒輪比;
                印字步進目標位置換算.Value = (int)Math.Round(axis0_pos,0);
                cnt++;
            }
            void cnt_Program_印字步進電子齒輪比_換算完成(ref int cnt)
            {
                印字步進換算完成.Bool = true;
                cnt++;
            }
        }
        #endregion
        #region 拉袋DA換算
        int cnt拉袋DA = 65534;
        List<PLC_Device> PLC_Devices_拉袋包數設定值 = new List<PLC_Device>();
        List<PLC_Device> PLC_Devices_拉袋DA設定值 = new List<PLC_Device>();
        List<PLC_Device> PLC_Devices_拉袋包數長度 = new List<PLC_Device>();
        bool[] 包數設定成立 = new bool[10];
        PLC_Device PLC_Devices_包藥拉袋次數累加值 = new PLC_Device("D1004");
        PLC_Device PLC_Devices_包藥拉袋次數設定值 = new PLC_Device("D1002");
        PLC_Device PLC_Devices_包藥拉袋累計長度 = new PLC_Device("D1006");
        PLC_Device PLC_Devices_包藥單包長度 = new PLC_Device("D12000");
        #region LIST佇列成員
        PLC_Device PLC_Devices_拉袋包數設定值01 = new PLC_Device("D1010");
        PLC_Device PLC_Devices_拉袋包數設定值02 = new PLC_Device("D1011");
        PLC_Device PLC_Devices_拉袋包數設定值03 = new PLC_Device("D1012");
        PLC_Device PLC_Devices_拉袋包數設定值04 = new PLC_Device("D1013");
        PLC_Device PLC_Devices_拉袋包數設定值05 = new PLC_Device("D1014");
        PLC_Device PLC_Devices_拉袋包數設定值06 = new PLC_Device("D1015");
        PLC_Device PLC_Devices_拉袋包數設定值07 = new PLC_Device("D1016");
        PLC_Device PLC_Devices_拉袋包數設定值08 = new PLC_Device("D1017");
        PLC_Device PLC_Devices_拉袋包數設定值09 = new PLC_Device("D1018");
        PLC_Device PLC_Devices_拉袋包數設定值10 = new PLC_Device("D1019");

        PLC_Device PLC_Devices_拉袋DA設定值01 = new PLC_Device("D1030");
        PLC_Device PLC_Devices_拉袋DA設定值02 = new PLC_Device("D1031");
        PLC_Device PLC_Devices_拉袋DA設定值03 = new PLC_Device("D1032");
        PLC_Device PLC_Devices_拉袋DA設定值04 = new PLC_Device("D1033");
        PLC_Device PLC_Devices_拉袋DA設定值05 = new PLC_Device("D1034");
        PLC_Device PLC_Devices_拉袋DA設定值06 = new PLC_Device("D1035");
        PLC_Device PLC_Devices_拉袋DA設定值07 = new PLC_Device("D1036");
        PLC_Device PLC_Devices_拉袋DA設定值08 = new PLC_Device("D1037");
        PLC_Device PLC_Devices_拉袋DA設定值09 = new PLC_Device("D1038");
        PLC_Device PLC_Devices_拉袋DA設定值10 = new PLC_Device("D1039");

        PLC_Device PLC_Devices_拉袋包數長度01 = new PLC_Device("D1050");
        PLC_Device PLC_Devices_拉袋包數長度02 = new PLC_Device("D1051");
        PLC_Device PLC_Devices_拉袋包數長度03 = new PLC_Device("D1052");
        PLC_Device PLC_Devices_拉袋包數長度04 = new PLC_Device("D1053");
        PLC_Device PLC_Devices_拉袋包數長度05 = new PLC_Device("D1054");
        PLC_Device PLC_Devices_拉袋包數長度06 = new PLC_Device("D1055");
        PLC_Device PLC_Devices_拉袋包數長度07 = new PLC_Device("D1056");
        PLC_Device PLC_Devices_拉袋包數長度08 = new PLC_Device("D1057");
        PLC_Device PLC_Devices_拉袋包數長度09 = new PLC_Device("D1058");
        PLC_Device PLC_Devices_拉袋包數長度10 = new PLC_Device("D1059");
        #endregion

        void sub_拉袋DA值線性減少換算()
        {

            if (cnt拉袋DA == 65534)
            {
                #region LIST佇列ADD
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值01);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值02);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值03);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值04);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值05);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值06);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值07);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值08);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值09);
                PLC_Devices_拉袋包數設定值.Add(PLC_Devices_拉袋包數設定值10);

                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值01);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值02);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值03);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值04);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值05);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值06);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值07);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值08);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值09);
                PLC_Devices_拉袋DA設定值.Add(PLC_Devices_拉袋DA設定值10);

                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度01);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度02);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度03);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度04);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度05);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度06);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度07);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度08);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度09);
                PLC_Devices_拉袋包數長度.Add(PLC_Devices_拉袋包數長度10);
                #endregion
                cnt拉袋DA = 65535;
            }
            if (cnt拉袋DA == 65535) cnt拉袋DA = 1;
            if (cnt拉袋DA == 1) cnt_Program_拉袋DA值線性減少換算_檢查按下(ref cnt拉袋DA);
            if (cnt拉袋DA == 2) cnt_Program_拉袋DA值線性減少換算_初始化(ref cnt拉袋DA);
            if (cnt拉袋DA == 3) cnt_Program_拉袋DA值線性減少換算_開始換算(ref cnt拉袋DA);
            if (cnt拉袋DA == 4) cnt_Program_拉袋DA值線性減少換算_換算完成(ref cnt拉袋DA);
            if (cnt拉袋DA == 5) cnt拉袋DA = 65500;
            if (cnt拉袋DA == 65500)
            {
                cnt拉袋DA = 65535;
            }
            void cnt_Program_拉袋DA值線性減少換算_檢查按下(ref int cnt)
            {
                if (印字步進換算開始.Bool) cnt拉袋DA++;
            }
            void cnt_Program_拉袋DA值線性減少換算_初始化(ref int cnt)
            {
                包數設定成立 = new bool[10];
            }
            void cnt_Program_拉袋DA值線性減少換算_開始換算(ref int cnt)
            {
                for (int i = 0; i <= 10; i++)
                {
                    PLC_Devices_拉袋包數長度[i].Value = PLC_Devices_拉袋包數設定值[i].Value * PLC_Devices_包藥單包長度.Value;
                }

                cnt拉袋DA++;
            }
            void cnt_Program_拉袋DA值線性減少換算_換算完成(ref int cnt)
            {
                印字步進換算完成.Bool = true;
                cnt拉袋DA++;
            }




        }
        #endregion
        #region 關閉程式停止輸出
        PLC_Device PLC_Device_左加熱BUT = new PLC_Device("S2");
        PLC_Device PLC_Device_右加熱BUT = new PLC_Device("S3");
        PLC_Device PLC_Device_左加熱 = new PLC_Device("Y2");
        PLC_Device PLC_Device_右加熱 = new PLC_Device("Y3");
        PLC_Device PLC_Device_左加熱TRI = new PLC_Device("S1010");
        PLC_Device PLC_Device_右加熱TRI = new PLC_Device("S1015");
        PLC_Device PLC_Device_左右加熱BUT = new PLC_Device("S1500");
        PLC_Device PLC_Device_左右加熱TRI = new PLC_Device("S1501");
        PLC_Device PLC_Device_拉袋馬達電源 = new PLC_Device("Y15");
        PLC_Device PLC_Device_拉袋馬達電源TRI = new PLC_Device("S1065");

        PLC_Device PLC_Device_三色燈綠TRI = new PLC_Device("M20");
        PLC_Device PLC_Device_三色燈橘TRI = new PLC_Device("M17");
        PLC_Device PLC_Device_三色燈紅TRI = new PLC_Device("M16");
        PLC_Device PLC_Device_三色燈蜂鳴TRI = new PLC_Device("M21");
        PLC_Device PLC_Device_三色燈綠 = new PLC_Device("Y20");
        PLC_Device PLC_Device_三色燈橘 = new PLC_Device("Y17");
        PLC_Device PLC_Device_三色燈紅 = new PLC_Device("Y16");
        PLC_Device PLC_Device_三色燈蜂鳴 = new PLC_Device("Y21");
        PLC_Device PLC_Device_無警報 = new PLC_Device("S8400");
        PLC_Device PLC_Device_關三色燈 = new PLC_Device("S8401");

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {

            while (true)
            {
                PLC_Device_左加熱BUT.Bool = false;
                PLC_Device_右加熱BUT.Bool = false;
                PLC_Device_左加熱.Bool = false;
                PLC_Device_左加熱TRI.Bool = false;
                PLC_Device_右加熱.Bool = false;
                PLC_Device_右加熱TRI.Bool = false;
                PLC_Device_左右加熱BUT.Bool = false;
                PLC_Device_左右加熱TRI.Bool = false;
                PLC_Device_拉袋馬達電源.Bool = false;
                PLC_Device_拉袋馬達電源TRI.Bool = false;
                PLC_Device_三色燈綠.Bool = false;
                PLC_Device_三色燈橘.Bool = false;
                PLC_Device_三色燈紅.Bool = false;
                PLC_Device_三色燈蜂鳴.Bool = false;
                PLC_Device_三色燈綠TRI.Bool = false;
                PLC_Device_三色燈橘TRI.Bool = false;
                PLC_Device_三色燈紅TRI.Bool = false;
                PLC_Device_三色燈蜂鳴TRI.Bool = false;
                PLC_Device_無警報.Bool = true;
                PLC_Device_關三色燈.Bool = true;

                if (!PLC_Device_左加熱.Bool && !PLC_Device_右加熱.Bool && !PLC_Device_左加熱TRI.Bool && !PLC_Device_右加熱TRI.Bool
                    && !PLC_Device_拉袋馬達電源TRI.Bool && !PLC_Device_拉袋馬達電源.Bool && !PLC_Device_左加熱BUT.Bool && !PLC_Device_右加熱BUT.Bool
                    && !PLC_Device_左右加熱BUT.Bool && !PLC_Device_左右加熱TRI.Bool && !PLC_Device_三色燈綠.Bool && !PLC_Device_三色燈橘.Bool
                    && !PLC_Device_三色燈紅.Bool && !PLC_Device_三色燈蜂鳴.Bool && !PLC_Device_三色燈綠TRI.Bool && !PLC_Device_三色燈橘TRI.Bool
                    && !PLC_Device_三色燈紅TRI.Bool && !PLC_Device_三色燈蜂鳴TRI.Bool && PLC_Device_無警報.Bool && PLC_Device_關三色燈.Bool)
                {
                    break;
                }


            }


        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {

        }


        #endregion

        DialogResult DialogResult_換袋歸零;
        PLC_Device PLC_Device_換袋 = new PLC_Device("S5000");
        PLC_Device PLC_Device_換袋中 = new PLC_Device("S5001");
        private void plC_Button9_btnClick(object sender, EventArgs e)
        {
            if(!PLC_Device_換袋.Bool)
            {
                DialogResult_換袋歸零 = MessageBox.Show("請確認是否換袋，累計數量歸零", "Notice", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

                if (DialogResult_換袋歸零 == DialogResult.OK)
                {
                    PLC_Device_換袋.Bool = true;

                }
            }
            else
            {
                PLC_Device_換袋中.Bool = false;
                PLC_Device_換袋.Bool = false;
            }
        }


    }
}
