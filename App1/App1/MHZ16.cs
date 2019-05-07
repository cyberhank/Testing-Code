﻿/*
Version:
V0.5
Release Date:
2017-01-17
Author:
Peng Wei
Lisence:
CC BY-NC-SA 3.0
Please keep the above information when you use this code in your project.
*/

// all of the following code was edited down from a github library for c sharp MHZ16 control

using System;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using Windows.Devices.I2c;

namespace App1
{
    public class MHZ16
    {
        private byte ADAPTOR_I2C_ADDR = 0x08; /* 7-bit I2C address of the Adaptor */
        private const byte IOCONTROL = (byte)(0x0E << 3);
        private const byte FCR = (byte)(0x02 << 3);
        private const byte LCR = (byte)(0x03 << 3);
        private const byte DLL = (byte)(0x00 << 3);
        private const byte DLH = (byte)(0x01 << 3);
        private const byte THR = (byte)(0x00 << 3);
        private const byte RHR = (byte)(0x00 << 3);
        private const byte TXLVL = (byte)(0x08 << 3);
        private const byte RXLVL = (byte)(0x09 << 3);
        private const byte SPR = (byte)(0x07 << 3);
        private byte[] CMD_READ_CONCENTRATION = { 0xFF, 0x01, 0x9C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x63 };

        public I2cDevice Adaptor;
        //private Timer PeriodicTimer;

        private byte[] Response = new byte[9];
        private int Concentration;
        private String TextError = "";
        //public MainPage app;

        public MHZ16()
        {


            /* Initialize the I2C bus, accelerometer, and timer */
            //Initialize();
        }

        public async void Initialize()
        {
            //app = new MainPage();

            var settings = new I2cConnectionSettings(ADAPTOR_I2C_ADDR);
            settings.BusSpeed = I2cBusSpeed.FastMode;
            var controller = await I2cController.GetDefaultAsync();
            Adaptor = controller.GetDevice(settings);

            /* Reset Adaptor */
            try
            {
                WriteRegister(IOCONTROL, 0x08);
            }
            catch
            {
                /* ignore NACK exception after software reset */
            }

            /* Ping Adaptor */
            try
            {
                WriteRegister(SPR, 0x5A);

                if (ReadRegister(SPR) != 0x5A)
                {
                    Debug.WriteLine("MHZ16 Ping Failed");
                   
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("I2C Communication Failed with MHZ16: " + ex.Message);
                return;
            }

            /* Configure Adaptor (FIFO, Baudrate setting etc.) */
            try
            {
                WriteRegister(FCR, 0x07);
                WriteRegister(LCR, 0x83);
                WriteRegister(DLL, 0x60);
                WriteRegister(DLH, 0x00);
                WriteRegister(LCR, 0x03);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to Initialize MHZ16 Adaptor: " + ex.Message);
                return;
            }

            /* Now that everything is initialized, create a timer so we read concentration every 2 seconds */
            //PeriodicTimer = new Timer(this.TimerCallback, null, 0, 2000);
        }

        //private void MainPage_Unloaded(object sender, object args)
        //{
        //    /* Cleanup */
        //    Adaptor.Dispose();
        //}

        //private void TimerCallback(object state)
        //{
        //    String textConcentration = "ERROR";
        //    String textResponse = "";

        //    if (ReadConcentration())
        //    {
        //        textConcentration = String.Format("{0:F0}", Concentration);
        //        textResponse = String.Format("[{0:F3}]: {1}", Environment.TickCount / 1000, BitConverter.ToString(Response));
        //    }

        //    /* UI updates must be invoked on the UI thread */
        //    var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        //    {
        //        Text_Concentration.Text = textConcentration;
        //        Text_Response.Text = textResponse;
        //        Text_Error.Text = TextError;
        //    });
        //}

        public double ReadCO2()
        {
            byte[] rhr = { RHR };
            int start;

            try
            {
                /* Clear Tx and Rx FIFO */
                WriteRegister(FCR, 0x07);

                /* Send command to read concentration */
                SendCommand(CMD_READ_CONCENTRATION);

                /* Mark a time in order to avoid dead loop */
                start = Environment.TickCount;

                while (true)
                {
                    if (ReadRegister(RXLVL) >= 9)
                    {
                        Adaptor.WriteRead(rhr, Response);

                        if (ValidateResponse())
                        {
                            Concentration = (Response[2] << 24) + (Response[3] << 16) + (Response[4] << 8) + Response[5];
                            return (Concentration);
                            //TextError = "";
                            //return true;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (Environment.TickCount >= start)
                    {
                        if (Environment.TickCount - start > 200)
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (Environment.TickCount > 200)
                        {
                            break;
                        }
                    }
                }

                Debug.WriteLine("Commnication Error.");
                //return false;
                return (999999);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Commnication Error: " + ex.Message);
                //return false;
                return (999999);
            }
        }

        private byte ReadRegister(byte reg_addr)
        {
            byte[] RegAddrBuf = { reg_addr };
            byte[] ReadBuf = new byte[1];

            I2cTransferResult result = Adaptor.WriteReadPartial(RegAddrBuf, ReadBuf);

            if (result.Status == I2cTransferStatus.FullTransfer)
            {
                return ReadBuf[0];
            }
            else
            {
                return 0;
            }
        }

        private void WriteRegister(byte reg_addr, byte val)
        {
            byte[] WriteBuf = { reg_addr, val };

            Adaptor.Write(WriteBuf);
        }

        private void SendCommand(byte[] command)
        {
            byte[] WriteBuf = { THR };
            WriteBuf = WriteBuf.Concat(command).ToArray();

            if (ReadRegister(TXLVL) >= command.Length)
            {
                Adaptor.Write(WriteBuf);
            }
        }

        private bool ValidateResponse()
        {
            byte sum = 0;

            foreach (byte b in Response)
            {
                sum += b;
            }

            if (sum == 0xFF)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}