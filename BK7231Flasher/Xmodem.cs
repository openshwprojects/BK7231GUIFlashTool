using System;
using System.Threading;
using System.IO;
using System.IO.Ports;

// SUMMARY:
// Desktop version of XMODEM based on the full .NET Framework.
//
// Author:
// Adrian Abordo
// adrian.abordo@iggmoe.com
//
// FEATURES:
// Sender and Receive cancel a file transfer upon receipt of a single <CAN> byte, for compatibility with terminals that only send 1 <CAN> 
// byte per cancellation, like TeraTerm.
//
// When Sender or Receiver wishes to cancel a file transfer, they send multiple <CAN> bytes, for compatibility with those terminals that 
// require consecutive <CAN> bytes in order to cancel.
//
// An XModem-CRC or XModem-1K Receiver automatically reverts to the older XModem-Checksum if the Sender does not support the newer formats, 
// as outlined in Chuck Forsberg's official documentation.
//
// An XModem-1K Receiver can accept data packets that are a mixture of 128-bytes and/or 1024-bytes long. Chuck Forsberg's official documentation
// allows XModem-1K Senders to transmit data packets of either length, so for maximum compatibility with various implementations, this 
// XModem-1K Receiver is flexible as well.
// 
// However, to accomodate XModem-1K Receiver implementations that are not capable of receiving packets with different lengths, this XModem-1K 
// Sender always sends 1024 data bytes per packet.
//
// Receiver accepts both <EOT> and <EOF> as file termination bytes. The official documentation specifies <EOT> to indicate end of file, 
// but some MS-DOS and Microsoft implementations of XModem send <EOF> instead. Both values are accepted for maximum compatibility.
//
// This Sender uses <EOT> to indicate end of file. However, a custom byte value can be specified via EndOfFileByteToSend.
//
// This Sender automatically updates its variant (XModem-1K, XModem-CRC or XModem-Checksum) according to the file initiation byte received. This
// allows it to upgrade to a newer variant or downgrade to an older one according to the variant requested by the Receiver.
//
// TrimPaddingBytesFromEnd() can be used to remove trailing padding bytes that are normally added to the end of the file to create a uniform packet
// length. Removing trailing padding bytes is recommended to ensure that the received file is identical byte-for-byte to its original version.

namespace BK7231Flasher
{
    public class XMODEM
    {
        // TERMINAL PROGRAM OBSERVATIONS:
        //
        // TeraTerm UTF-8 Pro:
        // XModem-1K ALWAYS sends 1024 data bytes/packet.
        // When Receiver cancels, only 1 <CAN> byte is sent. The <CAN> byte is sent as a standalone byte outside a packet. The Sender simply stops transmitting
        // at the nearest whole packet, and no acknowledgement is returned.
        // When Sender cancels, no control bytes are sent. Transmission simply stops at the nearest whole packet. Only the Receiver interpacket timeout will abort 
        // the transfer on the Receiver side.
        //
        // HyperTerminal 7.0:
        // XModem-1K USUALLY sends 1024 data bytes/packet. However, it can also switch to 128-byte packets if the portion to send is short.
        // When Receiver cancels, 5 <CAN> bytes are sent. The Sender simply stops transmitting at the nearest whole packet, and no acknowledgement is returned.
        // When Sender cancels, 5 <CAN> bytes are sent. The Receiver does not acknowledge. (An <ACK> is sent, but it's actually in response to the final
        // whole packet sent, not the cancellation notification.)
        //
        // This Implementation:
        // For both Sender and Receiver, requesting a cancellation is done by sending <CAN> 5 times. If the Sender or Receiver receives at least 1 <CAN> byte, 
        // transfer will abort. If the Sender receives the cancellation, it will stop at the nearest whole packet. No acknowledgement is sent under any 
        // circumstances by any party, in response to a cancellation.  When transmitting over a a long distance noisy channel, such as a telephone line, 
        // aborting on a single <CAN> byte can be problemmatic due to other standalone control bytes possibly being corrupted into <CAN> and triggering an 
        // unintended abort. However, this implementation is intended to be operated by devices that are directly connected via a serial cable, and so the risk
        // of byte corruption is much less. Aborting on a single <CAN> allows this implementation to respond to terminal software that only sends 1 <CAN> 
        // like TeraTerm. However, if this is problematic, NumCancellationBytesRequired can be used to set the minimum number of cancellation bytes that
        // will result in cancellation.
        //
        // On G120 largest byte array size permissible = 786364 bytes


        // *********************************** BEGIN: RECEIVER CUSTOMIZABLE PARAMETERS ***********************************

        /// <summary>
        /// The Receiver is responsible for initiating a file transfer.
        /// It does this by sending a FileInitiationByte, which is NAK for XModem-Checksum and C for XModem-CRC and XModem-1K.
        /// If the receiver has sent a FileInitiationByte, yet it has not received the first packet within the timeout specified
        /// below, it should resend the FileInitiationByte.
        /// </summary>
        public int ReceiverFileInitiationRetryMillisec = 250;

        /// <summary>
        /// This is the maximum number of times the Receiver will send the FileInitiationByte if the Sender has not sent the first packet.
        /// If this limit is reached, what happens next will depend on the Receiver's variant.
        ///
        /// SITUATION 1A: The Receiver is requesting XModem-CRC or XModem-1K --AND-- FallBackAllowed == True
        /// A Receiver wishing to use the newer CRC or 1K protocol requests a file transfer by sending <C>.
        /// The Sender is supposed to respond by sending the first packet. However, if the Sender does not support either of these new variants,
        /// it will not recognize the <C> and will not respond. If the maximum number of file initiation attempts is reached, the Receiver 
        /// will fall back to the older XModem-Checksum protocol and send <NAK> as its FileInitiationByte instead. 
        /// The number of attempts is reset. If the limit is reached again and the Sender still has not sent the first packet, then the file 
        /// transfer is aborted.
        ///
        /// SITUATION 1B: The Receiver is requesting XModem-CRC or XModem-1K --AND-- FallBackAllowed == False
        /// If this limit is reached and the first packet has not been received, the Receiver will abort the file transfer.
        ///
        /// SITUATION 2: The Receiver is requesting XModem-Checksum
        /// If this limit is reached and the first packet has not been received, the Receiver will abort the file transfer.
        /// </summary>
        public int ReceiverFileInitiationMaxAttempts = 240;

        /// <summary>
        /// XModem-CRC and XModem-1K evolved from XModem-Checksum.
        /// The official specification states that a Receiver which is requesting XModem-CRC or XModem-1K must have the
        /// ability to "fall back" to XModem-Checksum in case the Sender does not support newer formats.
        ///
        /// When True, this flag allows fallback to occur.
        /// When False, fallback will not occur, and the Receiver will simply abort the file transfer if the newer protocols aren't supported.
        /// This flag only has an effect if the Receiver is configured for XModem-CRC or XModem-1K. It has no impact if the Receiver is 
        /// already configured for XModem-Checksum.
        /// </summary>
        public bool ReceiverFallBackAllowed = true;

        /// <summary>
        /// If the Receiver is expecting data from the Sender, this is the maximum amount of time it will wait before sending NAK to prompt 
        /// the Sender to either resend the packet or finish the file.
        /// </summary>
        public int ReceiverTimeoutMillisec = 10000;

        /// <summary>
        /// If the Sender has not responded to repeated NAK nagging after this number of attempts, then the Receiver should abort the transfer.
        /// </summary>
        public int ReceiverMaxConsecutiveRetries = 10;

        private int _NumCancellationBytesRequired = 1;
        /// This is the minimum number of cancellation bytes that a Sender or Receiver must receive in order to cancel the file transfer.
        /// If 0 or less is specified, CAN bytes are ignored and will not result in cancellation, in which case cancellation will depend 
        /// entirely on the timeout mechanism.
        public int NumCancellationBytesRequired
        {
            get { return _NumCancellationBytesRequired; }
            set
            {
                _NumCancellationBytesRequired = value;
            }
        }

        private int _NumCancellationBytesToSend = 5;
        /// <summary>
        /// This is the number of CAN bytes that a Sender or Receiver will transmit to the connected party if the user requests a cancellation.
        /// </summary>
        public int NumCancellationBytesToSend
        {
            get { return _NumCancellationBytesToSend; }
            set
            {
                if (value >= 0)
                    _NumCancellationBytesToSend = value;
                else
                    NumCancellationBytesToSend = 5;
            }
        }

        // *********************************** END: RECEIVER CUSTOMIZABLE PARAMETERS ***********************************

        // *********************************** BEGIN: SENDER CUSTOMIZABLE PARAMETERS ***********************************

        /// <summary>
        /// The Receiver checks each packet for errors. If a packet contains errors, the Receiver is supposed to send NAK, telling the Sender to resend the packet.
        /// This is the total number of times the Sender will resend the same packet whenever consecutive NAKs are received.
        /// If this limit is reached and the Receiver is still sending NAK, the communication channel is assumed to be terminally unreliable
        /// and file transfer should abort.
        /// </summary>
        public int MaxSenderRetries = 10;

        /// <summary>
        /// The Receiver is supposed to validate each packet with ACK or NAK.
        /// If validation has not been received from the Receiver for this amount of time, the packet is resent.
        /// </summary>
        public int SenderPacketRetryTimeoutMillisec = 15000;

        /// <summary>
        /// If the Sender has not heard ANYTHING from the Receiver after this amount of time has elapsed, the file transfer is aborted.
        /// </summary>
        public int SendInactivityTimeoutMillisec = 60000;

        // *********************************** END: SENDER CUSTOMIZABLE PARAMETERS ***********************************

        // *********************************** BEGIN: COMMON CUSTOMIZABLE PARAMETERS ***********************************

        // ASCII codes for common character constants.
        // These are exposed as public fields in case the user needs to customize them with nonstandard values.
        public byte SOH = 1;    // Sender begins each 128-byte packet with this header
        public byte STX = 2;    // Sender begins each 1024-byte packet with this header
        public byte ACK = 6;    // Receiver sends this to indicate packet was received successfully with no errors
        public byte NAK = 21;   // Receiver sends this to initiate XModem-Checksum file transfer -- OR -- indicate packet errors
        public byte C = 67;     // Receiver sends this to initiate XModem-CRC or XModem-1K file transfer
        public byte EOT = 4;    // Sender sends this to mark the end of file. Receiver must acknowledge receipt of this byte with <ACK>, otherwise Sender resends <EOT> 
        public byte SUB = 26;   // This is used as a padding byte in the original specification
        public byte CAN = 24;   // [Commonly used but unofficial] Sender or Receiver sends this byte to abort file transfer
        public byte EOF = 26;   // [Commonly used but unofficial] MS-DOS version of <EOT>

        /// <summary>
        /// Defines the number of data bytes in a nominal 1024-byte packet.
        /// This allows the user to redefine a custom packet size for non-standard XModem implementations.
        /// </summary>
        private int _Packet1024NominalSize = 1024;
        public int Packet1024NominalSize
        {
            get { return _Packet1024NominalSize; }
            set
            {
                if (value > 0)
                {
                    _Packet1024NominalSize = value;
                    DefineDataPacketTemplate();
                }
            }
        }

        /// <summary>
        /// Defines the number of data bytes in a nominal 128-byte packet.
        /// This allows the user to redefine a custom packet size for non-standard XModem implementations.
        /// </summary>
        private int _Packet128NominalSize = 128;
        public int Packet128NominalSize
        {
            get { return _Packet128NominalSize; }
            set
            {
                if (value > 0)
                {
                    _Packet128NominalSize = value;
                    DefineDataPacketTemplate();
                }
            }
        }

        // *********************************** END: COMMON CUSTOMIZABLE PARAMETERS ***********************************

        /// <summary>
        /// CONSTRUCTOR.
        /// </summary>
        /// <param name="port">SerialPort to use when sending or receiving.</param>
        /// <param name="variant">
        /// The particular flavor of XModem to use.
        /// See Variants enumeration for a description of each XModem variant.
        /// </param>
        /// <param name="paddingByte">
        /// If sending, this is the byte value that will be used to pad a packet if the data being sent is shorter than
        /// the required packet length. If omitted, defaults to SUB (byte decimal 26).
        /// </param>
        /// <param name="endOfFileByteToSend">
        /// If sending, this is the byte value that will be sent to indicate that the end-of-file has been reached.
        /// Of omitted, defaults to EOT (byte decimal 4). Some XModem receiver implementations may require that EOF
        /// is used instead to indicate end-of-file.
        /// </param>
        public XMODEM(SerialPort port, Variants variant, byte paddingByte = 26, byte endOfFileByteToSend = 4)
        {
            // Field initialization
            PaddingByte = paddingByte;
            EndOfFileByteToSend = endOfFileByteToSend;

            // Property Initialization
            Port = port;
            Variant = variant;
        }

        /// <summary>
        /// Cancels the file transfer operation (send or receive) and notifies the other party of the
        /// cancellation by transmitting CAN bytes.
        /// </summary>
        public void CancelFileTransfer()
        {
            // Abort what we're currently doing
            Abort();

            // Send cancellation bytes
            for (int k = 1; k <= _NumCancellationBytesToSend; k++)
            {
                Port.Write(new byte[] { CAN }, 0, 1);
            }

            _TerminationReason = TerminationReasonEnum.UserCancelled;
        }

        /// <summary>
        /// Describes the various flavors of XModem.
        /// 
        /// XModemChecksum: This is the original, classic version of XModem. It is also the slowest and most error-prone.
        /// 128 data bytes per packet with checksum error-detection 
        /// 132 bytes/packet total = 3 header bytes + 128 data bytes + 1 error-detection byte
        /// 
        /// XModemCRC: This similar to the original, except with CRC-16 error detection instead of a simple checksum for more reliability.
        /// 128 data bytes per packet with CRC-16 error-detection 
        /// 133 bytes/packet total = 3 header bytes + 128 data bytes + 2 error-detection bytes
        /// 
        /// XModem1K: This is an updated version of XModem-CRC, expanded to 1024 data bytes (though 128 data bytes are still accepted).
        /// 128 and/or 1024 data bytes per packet and CRC-16 error-detection 
        /// 133 bytes/packet total = 3 header bytes + 128 data bytes + 2 error-detection bytes
        /// 1029 bytes/packet total = 3 header bytes + 1024 data bytes + 2 error-detection bytes
        /// </summary>
        public enum Variants
        {
            XModemChecksum,
            XModemCRC,
            XModem1K
        };

        private Variants _Variant;
        /// <summary>
        /// Gets/sets the XModem flavor that this implementation adheres to.
        /// </summary>
        public Variants Variant
        {
            get { return _Variant; }
            set
            {
                _Variant = value;
                DefineDataPacketTemplate(); // In case user wants to send data
            }
        }

        /// <summary>
        /// XModem's possible operational states.
        /// </summary>
        private enum States
        {
            Inactive,                           // The object is neither Sending nor Receiving
            ReceiverFileInitiation,             // Receiver is sending the file initiation byte at regular intervals
            ReceiverHeaderSearch,               // Receiver is expecting SOH or STX packet header
            ReceiverBlockNumSearch,             // Receiver is expecting the block number
            ReceiverBlockNumComplementSearch,   // Receiver is expecting the block number complement
            ReceiverDataBytesSearch,            // Receiver is populating data bytes
            ReceiverErrorCheckSearch,           // Receiver is expecting 1-byte or 2-byte check value(s)
            SenderAwaitingFileInitiation,       // Sender is expecting file transmission request from Receiver
            SenderPacketSent,                   // Sender has sent a packet and is waiting for Receiver to validate it
            SenderAlertForPossibleCancellation, // Sender is not expecting anything in particular but can respond to a cancellation request if needed
            SenderAwaitingEndOfFileConfirmation // Sender has transmitted the end-of-file byte and is waiting for Receiver to acknowledge receipt of that byte
        }

        private States CurrentState = States.Inactive;

        /// <summary>
        /// Describes how the current XModem session has ended.
        /// </summary>
        public enum TerminationReasonEnum
        {
            TransferStillActiveNotTerminated,   // File transfer is still active and has not terminated yet
            UserCancelled,                      // User has cancelled the file transfer
            EndOfFile,                          // End-of-file was reached. This is the ideal outcome when sending or receiving.
            FileInitiationTimeout,              // Receiver has repeatedly requested file transfer to begin, but Sender has not responded
            CancelNotificationReceived,         // Transfer aborted because cancellation bytes were detected from the Sender or Receiver
            TooManyRetries,                     // Too many erroneous packets have been sent or received. Indicates corruption or total communication loss.
            NoResponseFromReceiver              // When Sending a file, the Receiver has become completely silent for an extended period.
        }

        public TerminationReasonEnum _TerminationReason = TerminationReasonEnum.TransferStillActiveNotTerminated;
        /// <summary>
        /// Describes under what conditions the file transfer has terminated.
        /// </summary>
        public TerminationReasonEnum TerminationReason
        {
            get { return _TerminationReason; }
        }

        /// <summary>
        /// PacketReceived event handler.
        /// </summary>
        /// <param name="sender">The XMODEM object that raised the event.</param>
        /// <param name="packet">The complete, validated data packet received.</param>
        /// <param name="endOfFileDetected">
        /// Boolean flag that indicates if this packet is the final packet expected.
        /// True if the end-of-file byte was received, and the current packet is therefore the last one.
        /// False if the end-of-file byte has not been received, and more packets are still expected.
        /// </param>
        public delegate void PacketReceivedEventHandler(XMODEM sender, byte[] packet, bool endOfFileDetected);
        public delegate void PacketSentEventHandler(int sentBytes, int total);

        /// <summary>
        /// Raised whenever a complete packet has been received.
        /// </summary>
        public event PacketReceivedEventHandler PacketReceived;
        public event PacketSentEventHandler PacketSent;

        /// <summary>
        /// Communication port that will be used to send and receive.
        /// </summary>
        public SerialPort Port;

        /// <summary>
        /// Performs blocking when the Receive() method is called by the user.
        /// </summary>
        private ManualResetEvent ReceiverUserBlock = new ManualResetEvent(false);

        private int _NumCancellationBytesReceived = 0;
        /// <summary>
        /// Tracks the number of CAN bytes that have currently been received by this Sender or Receiver.
        /// </summary>
        public int NumCancellationBytesReceived
        {
            get { return _NumCancellationBytesRequired; }
        }

        /// <summary>
        /// MemoryStream used to store all data received if user wants to receive data in one big lump.
        /// This remains null if user wants to receive data packet-by-packet instead.
        /// </summary>
        private MemoryStream AllDataReceivedBuffer;

        /// <summary>
        /// Initiates the file receive process. This method blocks until the file transfer has terminated.
        /// </summary>
        /// <param name="allDataReceivedBuffer">
        /// Optional MemoryStream object, which may be omitted. 
        /// By instantiating an empty MemoryStream object and supplying it as an argument, the user can collect all data
        /// received into a single data structure and process it in one big lump once the transfer has finished. 
        /// This is NOT recommended if the expected file size can exceed the contiguous memory capacity of the device. 
        /// If the expected file size is large, subscribing to the PacketReceived event is recommended so that processing
        /// can be done one packet at a time within the available memory.
        /// </param>
        /// <returns>
        /// A TerminationReasonEnum that describes under what conditions the receive process has terminated. This may
        /// be due to a successful EndOfFile being reached, or due to timeouts, errors, etc.
        /// </returns>
        public TerminationReasonEnum Receive(MemoryStream allDataReceivedBuffer = null)
        {
            // Initialize control variables
            _TerminationReason = TerminationReasonEnum.TransferStillActiveNotTerminated;
            Aborted = false;
            BlockNumExpected = 1;
            _NumFileInitiationBytesSent = 0;
            _NumCancellationBytesReceived = 0;
            Remainder = new byte[0];
            DataPacketNumBytesStored = 0;
            ExpectingFirstPacket = true;
            ValidPacketReceived = false;

            // Define file initiation byte according to variant
            if (_Variant == Variants.XModemChecksum)
                FileInitiationByteToSend = NAK;
            else
                FileInitiationByteToSend = C;

            // Initialize state
            CurrentState = States.ReceiverFileInitiation;

            // Open port if it isn't open already
            if (Port.IsOpen == false)
                Port.Open();

            // Clear out serial port buffers
            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();

            // If user wants to get all data in one big lump, initialize the buffer with whatever MemoryStream object
            // is passed. If a memorystream is omitted, this defaults to null, which means received data
            // will not be stored.
            AllDataReceivedBuffer = allDataReceivedBuffer;

            // Attach event handler
            Port.DataReceived += new SerialDataReceivedEventHandler(Port_DataReceived);

            // Begin file initiation
            if (ReceiverFileInitiationTimer == null)
                ReceiverFileInitiationTimer = new Timer(ReceiverFileInitiationRoutine, null, 0, ReceiverFileInitiationRetryMillisec);
            else
                ReceiverFileInitiationTimer.Change(0, ReceiverFileInitiationRetryMillisec);

            // Block here
            ReceiverUserBlock.Reset();
            ReceiverUserBlock.WaitOne();
            ReceiverUserBlock.Reset();

            return TerminationReason;
        }

        // The byte value that a Receiver sends when requesting file transfer:
        // XModem Checksum = <NAK>
        // XModem CRC = <C>
        // XModem 1K = <C>
        private byte FileInitiationByteToSend;

        /// <summary>
        /// Sends the file initiation byte at regular intervals to request start of transfer from Sender.
        /// </summary>
        private Timer ReceiverFileInitiationTimer;

        /// <summary>
        /// Timer callback for file initiation timer.
        /// </summary>
        /// <param name="notUsed"></param>
        private void ReceiverFileInitiationRoutine(object notUsed)
        {
            Port.Write(new byte[] { FileInitiationByteToSend }, 0, 1);
            NumFileInitiationBytesSent += 1;
        }

        // Tracks how many file initiation bytes have already been sent to the Sender
        private int _NumFileInitiationBytesSent = 0;
        public int NumFileInitiationBytesSent
        {
            get { return _NumFileInitiationBytesSent; }
            private set
            {
                _NumFileInitiationBytesSent = value;

                // Determine if Receiver should fall back to an older variant
                if (_NumFileInitiationBytesSent > ReceiverFileInitiationMaxAttempts)
                {
                    if (FileInitiationByteToSend == C && ReceiverFallBackAllowed == true)
                    {
                        // Fall back to older variant
                        FileInitiationByteToSend = NAK;
                        _NumFileInitiationBytesSent = 0;
                    }
                    else
                    {
                        Abort();
                        _TerminationReason = TerminationReasonEnum.FileInitiationTimeout;
                        ReceiverUserBlock.Set();
                    }
                }
            }
        }

        // Indicates whether a valid packet has been received and is fit to be forwarded to user
        private bool ValidPacketReceived = false;

        /// <summary>
        /// State machine dispatcher.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = sender as SerialPort;
            int numBytes = sp.BytesToRead;
            byte[] recv = new byte[numBytes];
            sp.Read(recv, 0, numBytes);

            if (numBytes > 0)
            {
                switch (CurrentState)
                {
                    // RECEIVER STATES:
                    case States.ReceiverFileInitiation:
                        // A response was received from the Sender after 1 or more file initiation bytes have been sent.

                        // Halt the file initiation sender
                        if (ReceiverFileInitiationTimer != null)
                            ReceiverFileInitiationTimer.Change(Timeout.Infinite, Timeout.Infinite);

                        // Start the timeout/NAK watchdog
                        if (ReceiverNAKWatchdog == null)
                            ReceiverNAKWatchdog = new Timer(NAKNag, null, ReceiverTimeoutMillisec, ReceiverTimeoutMillisec);
                        else
                            ReceiverNAKWatchdog.Change(ReceiverTimeoutMillisec, ReceiverTimeoutMillisec);

                        // Advance to the next stage and process the current data received
                        CurrentState = States.ReceiverHeaderSearch;
                        ReceiverPacketBuilder(recv);
                        break;

                    case States.ReceiverHeaderSearch:
                    case States.ReceiverBlockNumSearch:
                    case States.ReceiverBlockNumComplementSearch:
                    case States.ReceiverDataBytesSearch:
                    case States.ReceiverErrorCheckSearch:
                        ResetNAKWatchdog();
                        ReceiverPacketBuilder(recv);
                        break;


                    // SENDER STATES:
                    case States.SenderAwaitingFileInitiation:
                        ResetReceiverStillAliveWatchdog();
                        if (DetectCancellation(recv) == false)
                        {
                            // Determine if file initiation byte for CRC or Checksum error-checking received.
                            if (Array.IndexOf(recv, C) > -1 || Array.IndexOf(recv, NAK) > -1)
                            {
                                // If user originally specified older XModem-Checksum but <C> was received instead,
                                // upgrade variant to new XModem-1K (which can handle both 128 and 1024 data bytes)
                                // for maximum flexibility.
                                if (_Variant == Variants.XModemChecksum && Array.IndexOf(recv, C) > -1)
                                    Variant = Variants.XModem1K;

                                if (Array.IndexOf(recv, NAK) > -1)
                                    Variant = Variants.XModemChecksum;

                                CurrentState = States.SenderAlertForPossibleCancellation;
                                WaitForResponseFromReceiver.Set();
                            }
                        }
                        break;

                    case States.SenderPacketSent:
                        ResetReceiverStillAliveWatchdog();
                        if (DetectCancellation(recv) == false)
                        {
                            if (Array.IndexOf(recv, ACK) > -1)
                            {
                                // ACK received
                                ResetSenderPacketResponseWatchdog();
                                _SenderConsecutiveRetryAttempts = 0;
                                PacketSuccessfullySent = true;
                                _BlockNumToSend += 1;
                                WaitForResponseFromReceiver.Set();
                            }
                            else if (Array.IndexOf(recv, NAK) > -1)
                            {
                                // NAK received
                                ResetSenderPacketResponseWatchdog();
                                _SenderConsecutiveRetryAttempts += 1;
                                WaitForResponseFromReceiver.Set();
                            }
                        }
                        break;

                    case States.SenderAlertForPossibleCancellation:
                        ResetReceiverStillAliveWatchdog();
                        DetectCancellation(recv);
                        break;

                    case States.SenderAwaitingEndOfFileConfirmation:
                        ResetReceiverStillAliveWatchdog();
                        if (DetectCancellation(recv) == false)
                        {
                            if (Array.IndexOf(recv, ACK) > -1)
                            {
                                // ACK received
                                EndOfFileAcknowledgementReceived = true;
                                Abort();
                            }
                        }
                        break;

                }
            }
        }

        /// <summary>
        /// Enforces timeouts if no data is received or the packet repeatedly fails the checksum.
        /// This timer must be reset within a set amount of time or NAK is transmitted.
        /// </summary>
        private Timer ReceiverNAKWatchdog;

        /// <summary>
        /// Resets the NAK watchdog.
        /// </summary>
        private void ResetNAKWatchdog()
        {
            if (ReceiverNAKWatchdog != null)
                ReceiverNAKWatchdog.Change(ReceiverTimeoutMillisec, ReceiverTimeoutMillisec);

            ReceiverNumConsecutiveNAKSent = 0;
        }

        /// <summary>
        /// Timer callback for the NAK watchdog.
        /// </summary>
        /// <param name="notUsed"></param>
        private void NAKNag(object notUsed)
        {
            SendNAK();
        }

        private void SendNAK()
        {
            Port.Write(new byte[] { NAK }, 0, 1);
            ReceiverNumConsecutiveNAKSent += 1;
        }

        private void SendACK()
        {
            Port.Write(new byte[] { ACK }, 0, 1);
            ReceiverNumConsecutiveNAKSent = 0;
        }

        // Tracks how many consecutive <NAK> bytes have been sent
        public int _ReceiverNumConsecutiveNAKSent = 0;
        public int ReceiverNumConsecutiveNAKSent
        {
            get { return _ReceiverNumConsecutiveNAKSent; }
            set
            {
                _ReceiverNumConsecutiveNAKSent = value;

                if (_ReceiverNumConsecutiveNAKSent >= ReceiverMaxConsecutiveRetries)
                {
                    Abort();
                    _TerminationReason = TerminationReasonEnum.TooManyRetries;
                    ReceiverUserBlock.Set();
                }
            }
        }

        /// <summary>
        /// The expected data packet size as specified by the incoming packet header.
        /// </summary>
        private int ExpectedDataPacketSize;

        // Stores the value of a candidate block number
        private byte BlockNumReceived;

        // Stores the value of a candidate block number complement (255 minus block number)
        private byte BlockNumComplementCandidateReceived;

        private bool ExpectingFirstPacket = true;
        private byte BlockNumExpected = 1;

        private byte[] BytesToParse;
        private byte[] Remainder = new byte[0];

        // Stores data bytes received. It is instantiated to be the same length as the expected data packet size.
        private byte[] DataPacketReceived;

        // Keeps track of the number of bytes actually placed inside the current data packet
        private int DataPacketNumBytesStored = 0;

        // Stores the check value. This will contain 1 byte for XModem-Checksum, and 2 bytes for XModem-CRC or XModem-1K.
        private byte[] ErrorCheck;

        // Calculates CRC-16 CCITT checksum using polynomial (X^16 + X^12 + X^5	+ 1)
        private CRC16CCITT CRC = new CRC16CCITT(CRC16CCITT.InitialCrcValue.Zeros);

        private void ReceiverPacketBuilder(byte[] freshBytes)
        {
            // The DataReceived event is occasionally triggered even when there are no bytes to read, so guard against this
            if (freshBytes.Length == 0)
                return;

            // We only want this to run once per DataReceived event and only if we are searching for a valid header byte
            if (Remainder.Length > 0 && CurrentState == States.ReceiverHeaderSearch)
                BytesToParse = CombineArrays(Remainder, freshBytes);
            else
                BytesToParse = freshBytes;

            // This keeps track of the index positions that searches begin at
            int headerByteSearchStartIndex = 0;
            int searchStartIndex = 0;

            // Loop while we are within bounds of the bytes to parse
            while (searchStartIndex < BytesToParse.Length && headerByteSearchStartIndex < BytesToParse.Length)
            {

                if (CurrentState == States.ReceiverHeaderSearch)
                {
                    // Empty the remainder if populated, since it should have fulfilled its purpose prior to us
                    // reaching this point. We don't want the remainder to carry over into the next DataReceived event
                    // if we are in the header byte search state.
                    if (Remainder.Length > 0)
                        Remainder = new byte[0];

                    // Check for file termination tokens
                    if (BytesToParse[headerByteSearchStartIndex] == EOT ||
                        BytesToParse[headerByteSearchStartIndex] == EOF)
                    {
                        SendACK();
                        Abort();
                        _TerminationReason = TerminationReasonEnum.EndOfFile;

                        // If subscribed to, raise packet received event and indicate end of file reached
                        if (PacketReceived != null && ValidPacketReceived == true && DataPacketReceived != null && DataPacketReceived.Length > 0)
                            PacketReceived(this, DataPacketReceived, true);

                        ReceiverUserBlock.Set();
                        return;
                    }
                    else
                    {
                        // If subscribed to, raise packet received event and indicate end of file NOT yet reached
                        if (PacketReceived != null && ValidPacketReceived == true && DataPacketReceived != null && DataPacketReceived.Length > 0)
                            PacketReceived(this, DataPacketReceived, false);
                    }

                    // Reset packet received validation flag
                    ValidPacketReceived = false;

                    // Check for cancellation bytes
                    if (_NumCancellationBytesRequired > 0)
                    {
                        // Check if current byte is a cancellation request
                        if (BytesToParse[headerByteSearchStartIndex] == CAN)
                        {
                            _NumCancellationBytesReceived += 1;

                            if (_NumCancellationBytesReceived >= _NumCancellationBytesRequired)
                            {
                                // CANCEL THE FILE TRANSFER
                                Abort();
                                _TerminationReason = TerminationReasonEnum.CancelNotificationReceived;
                                ReceiverUserBlock.Set();
                                return;
                            }
                            else
                            {
                                // Move on to the next byte
                                headerByteSearchStartIndex += 1;
                                continue;
                            }
                        }
                        else
                        {
                            // If not a cancellation byte, reset the cancellation byte counter
                            _NumCancellationBytesReceived = 0;
                        }
                    }

                    // Determine if we have a candidate header byte based on our XModem variant
                    if (_Variant == Variants.XModemChecksum || _Variant == Variants.XModemCRC)
                    {
                        // XModem-Checksum and XModem-CRC should always have 128 data byte packets headed by <SOH>.
                        // Determine the location of this header byte, if it's present.                        
                        int foundIndex = Array.IndexOf(BytesToParse, SOH, headerByteSearchStartIndex);

                        if (foundIndex == -1) // No header byte found
                        {
                            // Quit this search and ignore the remaining bytes. 
                            // Look for a header byte in the next transmission.
                            return;
                        }
                        else if (foundIndex > -1)
                        {
                            // Save the index position where we THINK a valid header byte resides at.
                            // The candidate header byte may be discovered to be invalid later on, and we
                            // need an index to return to in case we need to repeat the header search
                            // starting from the next index.
                            headerByteSearchStartIndex = foundIndex + 1;

                            // Specify the packet size corresponding to this header
                            ExpectedDataPacketSize = _Packet128NominalSize;

                            // Move on to the next stage
                            searchStartIndex = foundIndex + 1;
                            CurrentState = States.ReceiverBlockNumSearch;
                            continue;
                        }
                    }
                    else if (_Variant == Variants.XModem1K)
                    {
                        // XModem-1K can receive 1024 data byte packets headed by <STX> --OR-- 128 data byte packets headed by <SOH>.
                        // The official standard allows a Sender to send a mixture of packet sizes, so a Receiver has to look for both.
                        int packetStartIndexSTX = Array.IndexOf(BytesToParse, STX, headerByteSearchStartIndex);
                        int packetStartIndexSOH = Array.IndexOf(BytesToParse, SOH, headerByteSearchStartIndex);

                        // Look for the packet header byte
                        int foundIndex = 0;
                        if (packetStartIndexSTX > -1 && packetStartIndexSOH > -1)
                        {
                            // There is a (slim) possibility that both <SOH> and <STX> bytes may be found.                      
                            // If both are found, our candidate packet start index should be the earlier of the two. 
                            // If we're wrong, we can always get to the later one during the next iteration.
                            if (packetStartIndexSTX <= packetStartIndexSOH)
                            {
                                // Header for 1024 data bytes/packet
                                ExpectedDataPacketSize = _Packet1024NominalSize;
                                foundIndex = packetStartIndexSTX;
                            }
                            else
                            {
                                // Header for 128 data bytes/packet
                                ExpectedDataPacketSize = _Packet128NominalSize;
                                foundIndex = packetStartIndexSOH;
                            }
                        }
                        else if (packetStartIndexSTX > -1)  // Only 1 found
                        {
                            // Header for 1024 data bytes/packet
                            ExpectedDataPacketSize = _Packet1024NominalSize;
                            foundIndex = packetStartIndexSTX;
                        }
                        else if (packetStartIndexSOH > -1)  // Only 1 found
                        {
                            // Header for 128 data bytes/packet
                            ExpectedDataPacketSize = _Packet128NominalSize;
                            foundIndex = packetStartIndexSOH;
                        }
                        else
                        {
                            // If neither candidate headers were found, quit this search and ignore the remaining bytes.
                            // Look for a header byte in the next transmission.
                            return;
                        }

                        // Save the index position where we THINK a valid header byte resides at.
                        // The candidate header byte may be discovered to be invalid later on, and we
                        // need an index to return to in case we need to repeat the header search
                        // starting from the next index.
                        headerByteSearchStartIndex = foundIndex + 1;

                        // Move on to the next stage
                        searchStartIndex = foundIndex + 1;
                        CurrentState = States.ReceiverBlockNumSearch;
                        continue;
                    }
                }

                if (CurrentState == States.ReceiverBlockNumSearch)
                {
                    // The block number should be immediately after the packet header byte
                    BlockNumReceived = BytesToParse[searchStartIndex];

                    // If the candidate block number is equal to a header byte, there is a slim possibility that what we currently believe
                    // is the block number may actually be the true header byte, especially if the current candidate header byte is discovered
                    // to be invalid later. Therefore, we should save the "block number" as a remainder so we can re-examine it later if 
                    // the current header byte canididate does not pan out and another DataReceived event is raised.
                    if ((_Variant == Variants.XModemChecksum || _Variant == Variants.XModemCRC)
                        && BlockNumReceived == SOH)
                    {
                        Remainder = new byte[] { BlockNumReceived };   // Initialize remainder
                    }
                    else if (_Variant == Variants.XModem1K && (BlockNumReceived == SOH || BlockNumReceived == STX))
                    {
                        Remainder = new byte[] { BlockNumReceived };   // Initialize remainder
                    }

                    // Move on to the next stage
                    searchStartIndex += 1;
                    CurrentState = States.ReceiverBlockNumComplementSearch;
                    continue;
                }

                if (CurrentState == States.ReceiverBlockNumComplementSearch)
                {
                    // The complement of the block number should be right after the block number
                    BlockNumComplementCandidateReceived = BytesToParse[searchStartIndex];

                    // Determine if we have received a valid block number and complement.
                    // This will determine if we have a valid packet header and can proceed to the next stage.
                    if (BlockNumComplementCandidateReceived == 255 - BlockNumReceived)
                    {
                        // Valid packet header....

                        // The packet header is valid, so we don't need to re-examine the remainder and can therefore
                        // discard it
                        if (Remainder.Length > 0)
                            Remainder = new byte[0];

                        // Instantiate the data packet array which will hold incoming data
                        DataPacketReceived = new byte[ExpectedDataPacketSize];
                        DataPacketNumBytesStored = 0;

                        // Move on to the next stage
                        searchStartIndex += 1;
                        CurrentState = States.ReceiverDataBytesSearch;
                        continue;
                    }
                    else
                    {
                        // Not a valid packet header....

                        // Add the candidate block number complement to the remainder if: 
                        // 1.) the remainder already contains an alternate candidate header byte --OR--
                        // 2.) the candidate block number complement has the same value as a header byte, and could therefore be a valid header byte itself
                        if (Remainder.Length > 0 || ((_Variant == Variants.XModemChecksum || _Variant == Variants.XModemCRC)
                            && BlockNumReceived == SOH))
                        {
                            Remainder = CombineArrays(Remainder, new byte[] { BlockNumComplementCandidateReceived });   // Add to remainder
                        }
                        else if (Remainder.Length > 0 || (_Variant == Variants.XModem1K && (BlockNumReceived == SOH || BlockNumReceived == STX)))
                        {
                            Remainder = CombineArrays(Remainder, new byte[] { BlockNumComplementCandidateReceived });   // Add to remainder
                        }

                        // Search for another header byte
                        CurrentState = States.ReceiverHeaderSearch;
                        continue;
                    }
                }

                if (CurrentState == States.ReceiverDataBytesSearch)
                {
                    // Begin filling the data packet....

                    // Determine if there are enough unparsed bytes to fill the data packet
                    int numUnparsedBytesRemaining = BytesToParse.Length - searchStartIndex;
                    int numDataPacketBytesStillMissing = DataPacketReceived.Length - DataPacketNumBytesStored;

                    int numDataBytesToPull;
                    if (numUnparsedBytesRemaining >= numDataPacketBytesStillMissing)
                        numDataBytesToPull = numDataPacketBytesStillMissing;
                    else
                        numDataBytesToPull = numUnparsedBytesRemaining;

                    Array.Copy(BytesToParse, searchStartIndex, DataPacketReceived, DataPacketNumBytesStored, numDataBytesToPull);
                    DataPacketNumBytesStored += numDataBytesToPull;
                    searchStartIndex += numDataBytesToPull;

                    if (DataPacketNumBytesStored >= ExpectedDataPacketSize)
                    {
                        // If all expected data bytes have been gathered, move on to the next stage
                        CurrentState = States.ReceiverErrorCheckSearch;
                        ErrorCheck = new byte[0];
                    }

                    continue;
                }

                if (CurrentState == States.ReceiverErrorCheckSearch)
                {
                    if (_Variant == Variants.XModemChecksum)
                    {
                        // 1 error-check byte expected.
                        ErrorCheck = new byte[] { BytesToParse[searchStartIndex] };

                        // Validate packet
                        ValidatePacket();

                        // Start over               
                        headerByteSearchStartIndex = searchStartIndex + 1;
                        CurrentState = States.ReceiverHeaderSearch;
                    }
                    else  // XModem-CRC or XModem-1K
                    {
                        // 2 error-check bytes expected.

                        // If we have not yet gathered the required number of error check bytes, append to the error check
                        // array.
                        if (ErrorCheck.Length < 2)
                        {
                            ErrorCheck = CombineArrays(ErrorCheck, new byte[] { BytesToParse[searchStartIndex] });
                        }

                        if (ErrorCheck.Length >= 2)
                        {
                            // We have enough error-check bytes, so validate packet
                            ValidatePacket();

                            // Return to the initial state and start over               
                            headerByteSearchStartIndex = searchStartIndex + 1;
                            CurrentState = States.ReceiverHeaderSearch;
                        }
                        else
                        {
                            // Advance to the next byte
                            searchStartIndex += 1;
                        }
                    }
                } // End if
            } // End while
        } // End method

        private void ValidatePacket()
        {
            // In order for a packet to be accepted, it must be an expected block number, and the transmitted
            // check value must match the calculated check value.

            // Dealing with a block number that is out of sequence:
            //
            // Normally, successive block numbers must be monotonically increasing (accounting for binary wraparound) in order
            // to be valid. <NAK> is normally sent if a block number is out of sequence. However, there's an exception.
            //
            // Exception:
            // If the current block number has been duplicated (it has the same value as a block number that was
            // received previously), <ACK> is sent anyway on the theory that the Sender may not have received
            // the previous <ACK> and decided to send the same packet again. <ACK> will prompt the Sender to move on to the
            // next packet, which is what we want. (<NAK> will make it resend the packet yet again, which is unwanted.)
            //
            // Exception to the exception:
            // If this is the very first packet, the block number MUST be 1. Otherwise, <NAK> will be sent.

            if (BlockNumReceived == BlockNumExpected)
            {
                if (ValidateChecksum() == true)
                {
                    // Update control variables
                    BlockNumExpected += 1;
                    ExpectingFirstPacket = false;

                    // If user wants all received data to be outputed in one lump, add this packet to the buffer
                    if (AllDataReceivedBuffer != null)
                        AllDataReceivedBuffer.Write(DataPacketReceived, 0, DataPacketReceived.Length);

                    ValidPacketReceived = true;

                    // Notify Sender to send the next packet
                    SendACK();
                }
                else
                {
                    // Inform sender that checksum invalid
                    SendNAK();
                    ValidPacketReceived = false;
                }
            }
            else if (ExpectingFirstPacket == false && BlockNumReceived == (byte)(BlockNumExpected - 1))
            {
                // Receiver got a duplicate packet.
                // Send <ACK> to prompt the Sender to advance to the next packet. Ignore the current (redundant) packet.
                SendACK();
                ValidPacketReceived = false;
            }
            else
            {
                // The block number is completely out of sequence, so send NAK
                SendNAK();
                ValidPacketReceived = false;
            }
        }

        /// <summary>
        /// Calculates the check value (simple checksum or CRC-16) appended to the end of a packet.
        /// </summary>
        /// <returns>
        /// True if the calculated check value and received check value match.
        /// False if there is a mismatch between the calculated and received check values.
        /// </returns>
        private bool ValidateChecksum()
        {
            switch (_Variant)
            {
                // Arithmetic checksum:
                case Variants.XModemChecksum:
                    byte checksum = CheckSum(DataPacketReceived);
                    if (checksum == ErrorCheck[0])
                        return true;
                    else
                        return false;

                // CRC-16:
                case Variants.XModemCRC:
                case Variants.XModem1K:
                    ushort crcChecksumCalculated = CRC.ComputeChecksum(DataPacketReceived);
                    ushort crcChecksumReceived = BytesToUShort(ErrorCheck[0], ErrorCheck[1]);
                    if (crcChecksumCalculated == crcChecksumReceived)
                        return true;
                    else
                        return false;

                // Just so VS2010 doesn't complain:
                default:
                    return false;
            }
        }

        private ushort BytesToUShort(byte highByte, byte lowByte)
        {
            return (ushort)((highByte << 8) + lowByte);
        }

        private byte[] UShortToBytes(ushort val)
        {
            byte highByte = (byte)(val / 256);
            byte lowByte = (byte)(val % 256);

            return new byte[] { highByte, lowByte };
        }

        /// <summary>
        /// Calculates the simple checksum by summing all values in a byte array and returning the remainder.
        /// </summary>
        /// <param name="seq">
        /// Byte array whose checksum to calculate.
        /// </param>
        /// <returns>
        /// Modulo-256 checksum.
        /// </returns>
        private byte CheckSum(byte[] seq)
        {
            byte sum = 0;
            for (int k = 0; k < seq.Length; k++)
            {
                sum += seq[k];
            }
            return sum;
        }

        private bool Aborted = false;

        /// <summary>
        /// Internal method used to cancel the file transfer.
        /// </summary>
        private void Abort()
        {
            CurrentState = States.Inactive;
            TerminateSend = true;
            SenderInitialized = false;
            Aborted = true;

            // Detach event handler
            Port.DataReceived -= Port_DataReceived;

            // If we are sending data, tell Sender not to expect any more responses from Receiver.
            // This has no ill effect if we are receiving instead.
            WaitForResponseFromReceiver.Set();

            // Deactivate Send and Receive watchdogs
            if (ReceiverNAKWatchdog != null)
                ReceiverNAKWatchdog.Change(Timeout.Infinite, Timeout.Infinite);

            if (ReceiverFileInitiationTimer != null)
                ReceiverFileInitiationTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (SenderPacketResponseWatchdog != null)
                SenderPacketResponseWatchdog.Change(Timeout.Infinite, Timeout.Infinite);

            if (ReceiverStillAliveWatchdog != null)
                ReceiverStillAliveWatchdog.Change(Timeout.Infinite, Timeout.Infinite);

            // Flush serial data so they don't contaminate a future session
            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();
        }

        // ************************************* SENDER SENDER SENDER SENDER SENDER SENDER SENDER ***********************************

        private ManualResetEvent WaitForResponseFromReceiver = new ManualResetEvent(false);

        private Timer SenderPacketResponseWatchdog;

        private Timer ReceiverStillAliveWatchdog;

        /// <summary>
        /// This is an array that will be instantiated to the specified data packet size and filled with padding bytes.
        /// The intent is for this array to be copied for each outgoing data packet and populated with data bytes.
        /// This avoids the overhead of having to populate new arrays with padding bytes each time.
        /// </summary>
        private byte[] SenderDataPacketMasterTemplate;

        private byte[] DataPacketToSend;

        private bool TerminateSend = false;

        /// <summary>
        /// Tracks the number of data bytes that have currently been added to the outbound packet.
        /// </summary>
        private int NumUserDataBytesAddedToCurrentPacket = 0;

        /// <summary>
        /// The byte value used to pad a packet in order to meet its 128-byte or 1024-byte required length.
        /// </summary>
        public byte PaddingByte;

        /// <summary>
        /// When using this XModem to send data, the official specification requires that EOT is transmitted to signal the end of file.
        /// However, some programs may require a different byte value, such as EOF instead. This allows the user to specify
        /// a custom byte value to transmit to the Receiver when the file is complete.
        /// </summary>
        public byte EndOfFileByteToSend;

        /// <summary>
        /// Defines a master outbound data packet that will be filled with padding bytes.
        /// The purpose of this template is to be copied for each new outbound packet and subsequently
        /// populated with actual data.
        /// </summary>
        private void DefineDataPacketTemplate()
        {
            int dataPacketSize;
            if (_Variant == Variants.XModem1K)
                dataPacketSize = _Packet1024NominalSize;
            else
                dataPacketSize = _Packet128NominalSize;

            SenderDataPacketMasterTemplate = new byte[dataPacketSize];

            // Fill the template with padding bytes
            for (int k = 0; k < SenderDataPacketMasterTemplate.Length; k++)
                SenderDataPacketMasterTemplate[k] = PaddingByte;
        }

        private byte _BlockNumToSend = 1;
        public byte BlockNumToSend
        {
            get { return _BlockNumToSend; }
        }

        /// <summary>
        /// Initializes the modem send process.
        /// </summary>
        /// <param name="dataToSend">
        /// Optional argument. 
        /// If provided, this is the file that should be transmitted in its entirety.
        /// The EndOfFile byte is automatically transmitted once this array is finished.
        /// If omitted, the file may be sent piece-by-piece using the AddToOutboundPacket() method.
        /// </param>
        /// <returns>
        /// The number of data bytes successfully transmitted.
        /// </returns>
        public int Send(byte[] dataToSend = null)
        {
            // Initialize control variables
            _TerminationReason = TerminationReasonEnum.TransferStillActiveNotTerminated;
            Aborted = false;
            _BlockNumToSend = 1;    // Current outbound block number
            _SenderConsecutiveRetryAttempts = 0;
            PacketSuccessfullySent = false;
            DataPacketToSend = null;
            NumUserDataBytesAddedToCurrentPacket = 0;
            _TotalUserDataBytesPacketized = 0;
            _TotalUserDataBytesSent = 0;
            _NumCancellationBytesReceived = 0;
            TerminateSend = false;
            EndOfFileAcknowledgementReceived = false;
            SenderInitialized = true;   // Ensures that this method is called first before AddToOutboundPacket()

            WaitForResponseFromReceiver.Reset();
            CurrentState = States.SenderAwaitingFileInitiation;

            // Open port if it isn't open already
            if (Port.IsOpen == false)
                Port.Open();

            Port.DiscardInBuffer();
            Port.DiscardOutBuffer();

            if (ReceiverStillAliveWatchdog == null)
                ReceiverStillAliveWatchdog = new Timer(ReceiverStillAliveWatchdogRoutine, null, SendInactivityTimeoutMillisec, SendInactivityTimeoutMillisec);
            else
                ReceiverStillAliveWatchdog.Change(SendInactivityTimeoutMillisec, SendInactivityTimeoutMillisec);

            if (SenderPacketResponseWatchdog == null)
                SenderPacketResponseWatchdog = new Timer(SenderPacketResponseWatchdogRoutine, null, SenderPacketRetryTimeoutMillisec, SenderPacketRetryTimeoutMillisec);
            else
                SenderPacketResponseWatchdog.Change(SenderPacketRetryTimeoutMillisec, SenderPacketRetryTimeoutMillisec);

            // Attach event handler
            Port.DataReceived += new SerialDataReceivedEventHandler(Port_DataReceived);

            // Wait here for file initiation byte to be received from Receiver
            WaitForResponseFromReceiver.WaitOne();
            WaitForResponseFromReceiver.Reset();

            if (dataToSend != null && Aborted == false)
            {
                AddToOutboundPacket(dataToSend);
                if (TerminateSend == false)
                    EndFile();

                return _TotalUserDataBytesSent;
            }
            else
                return 0;
        }

        private int _TotalUserDataBytesPacketized = 0;
        /// <summary>
        /// Returns the total number of user data bytes that have been incorporated into outbound packets since the current
        /// send session was started. This counts data bytes that have already been successfully transmitted as well as
        /// those data bytes that have been added to a packet that is still waiting for completion. Header bytes, padding bytes,
        /// checksum bytes (any overhead incurred by the XModem infrastructure) are NOT included. 
        /// 
        /// This allows the user, for example, to calculate the proper file offset if reading an extremely large file
        /// from persistent storage. 
        /// </summary>
        public int TotalUserDataBytesPacketized
        {
            get { return _TotalUserDataBytesPacketized; }
        }

        private int _TotalUserDataBytesSent = 0;
        /// <summary>
        /// Returns the total number of user data bytes that have been successfully transmitted since the current send session
        /// was started. Only data bytes are included in this count. Header bytes, padding bytes, checksum bytes (any overhead 
        /// incurred by the XModem infrastructure) are NOT included. 
        /// 
        /// When the transfer has terminated, this can also tell the user if the file was successfully sent in its entirety.
        /// </summary>
        public int TotalUserDataBytesSent
        {
            get { return _TotalUserDataBytesSent; }
        }

        /// <summary>
        /// Sentinel variable that verifies that Send() method is called before AddToOutboundPacket().
        /// </summary>
        private bool SenderInitialized = false;

        /// <summary>
        /// Assembles one or more packets from the bytes passed to this method and automatically transmits them when the
        /// packet size has been satisfied.
        /// 
        /// The number of bytes supplied by the user during each call may be completely arbitrary. Supplied arguments may be 
        /// extremely short or extremely long. This method automatically parses the supplied byte arrays and builds packets
        /// on the fly. Once enough data has been received for a packet, that packet is automatically transmitted.
        /// 
        /// This method is useful when reading a very large file piece-by-piece from a source, and for packetizing those
        /// pieces automatically.
        /// </summary>
        /// <param name="dataToSend">
        /// The data bytes that should be added to the current pending packet. This array may be any length.
        /// </param>
        /// <returns>
        /// The number of data bytes successfully sent during this particular method call. 
        /// This is 0 if the packet is still too short to send using the data bytes collected thus far. By knowing how many data 
        /// bytes were successfully transmitted during this method invocation, the user can calculate the correct file offset
        /// if the source file is extremely lage and is being read from persistent storage, for example.
        /// 
        /// Note that only the data bytes successfully transmitted during THIS method call are counted. Use the
        /// TotalUserDataBytesSent property to keep track of the accumulated total of all user data bytes sent during the
        /// current send session.
        /// </returns>
        public int AddToOutboundPacket(byte[] dataToSend)
        {
            // Tracks the number of user data bytes that have been successfully sent during this method invocation
            int numUnpaddedDataBytesSentThisCall = 0;

            // Ensure that the Send() method is first called before this method
            if (SenderInitialized == false)
            {
                throw new ArgumentException("The XMODEM.Send() method must first be called before XMODEM.AddToOutboundPacket() is used.");
            }

            int dataOffset = 0;
            while (dataOffset < dataToSend.Length && TerminateSend == false)
            {

                // Instantiate outbound data packet if empty
                if (DataPacketToSend == null)
                {
                    if (_Variant == Variants.XModem1K)
                        DataPacketToSend = new byte[_Packet1024NominalSize];
                    else
                        DataPacketToSend = new byte[_Packet128NominalSize];

                    Array.Copy(SenderDataPacketMasterTemplate, DataPacketToSend, DataPacketToSend.Length);
                }

                int numUnparsedDataBytes = dataToSend.Length - dataOffset;
                int numPacketDataBytesNeeded = DataPacketToSend.Length - NumUserDataBytesAddedToCurrentPacket;

                int numBytesToAdd;
                if (numPacketDataBytesNeeded >= numUnparsedDataBytes)
                    numBytesToAdd = numUnparsedDataBytes;
                else
                    numBytesToAdd = numPacketDataBytesNeeded;

                Array.Copy(dataToSend, dataOffset, DataPacketToSend, NumUserDataBytesAddedToCurrentPacket, numBytesToAdd);

                NumUserDataBytesAddedToCurrentPacket += numBytesToAdd;
                dataOffset += numBytesToAdd;
                _TotalUserDataBytesPacketized += numBytesToAdd;

                if (NumUserDataBytesAddedToCurrentPacket >= DataPacketToSend.Length)
                {
                    TransmitPacket();

                    // Determine if packet transmission was successful, or the maximum number of retries has been exhausted:
                    if (PacketSuccessfullySent == true)
                    {
                        // If packet successfully transmitted, keep a running tally of data bytes sent out.
                        // Only count actual user-provided data. Padding bytes are not counted.
                        _TotalUserDataBytesSent += NumUserDataBytesAddedToCurrentPacket;
                        numUnpaddedDataBytesSentThisCall += NumUserDataBytesAddedToCurrentPacket;
                        PacketSent?.Invoke(_TotalUserDataBytesSent, dataToSend.Length);
                        
                        // Reset control variables
                        NumUserDataBytesAddedToCurrentPacket = 0;

                        // Re-initialize a new packet
                        DataPacketToSend = null;
                    }
                    else if (TerminateSend == false)
                    {
                        // Terminal condition if ACK not received even after multiple attempts
                        Abort();
                        _TerminationReason = TerminationReasonEnum.TooManyRetries;
                        break;
                    }
                }
            }

            return numUnpaddedDataBytesSentThisCall;
        }

        private bool PacketSuccessfullySent = false;

        private int _SenderConsecutiveRetryAttempts = 0;
        public int SenderConsecutiveRetryAttempts
        {
            get { return _SenderConsecutiveRetryAttempts; }
        }

        private void TransmitPacket()
        {
            // Calculate check-value
            byte[] checkValueBytes;
            if (_Variant == Variants.XModemChecksum)
                checkValueBytes = new byte[] { CheckSum(DataPacketToSend) };
            else
            {
                ushort checkValueShort = CRC.ComputeChecksum(DataPacketToSend);
                checkValueBytes = UShortToBytes(checkValueShort);
            }

            // Determine packet size header
            byte packetSizeHeader;
            if (_Variant == Variants.XModem1K)
                packetSizeHeader = STX;
            else
                packetSizeHeader = SOH;

            PacketSuccessfullySent = false;
            while (PacketSuccessfullySent == false && _SenderConsecutiveRetryAttempts < MaxSenderRetries && TerminateSend == false)
            {
                // Send packet size header
                Port.Write(new byte[] { packetSizeHeader }, 0, 1);

                // Send block number
                Port.Write(new byte[] { _BlockNumToSend }, 0, 1);

                // Send block number complement
                Port.Write(new byte[] { (byte)(255 - _BlockNumToSend) }, 0, 1);

                // Send data packet
                Port.Write(DataPacketToSend, 0, DataPacketToSend.Length);

                // Update state. Do this just before the Receiver is expected to respond so its response
                // doesn't fall through the cracks if it replies extremely quickly.
                WaitForResponseFromReceiver.Reset();
                CurrentState = States.SenderPacketSent;

                // Send check-value. This completes the packet. A response from the Receiver is expected after this.
                Port.Write(checkValueBytes, 0, checkValueBytes.Length);

                // Wait for ACK or NAK or CAN
                WaitForResponseFromReceiver.WaitOne();
                WaitForResponseFromReceiver.Reset();

                // Once we've gotten a response, the only message expected from the Receiver at this point is a possible cancellation
                CurrentState = States.SenderAlertForPossibleCancellation;
            }
        }

        /// <summary>
        /// Determines if the minimum number of consecutive cancellation bytes are present in a byte array.
        /// </summary>
        /// <param name="recv">
        /// The byte array to search for consecutive cancellation bytes.
        /// </param>
        /// <returns>
        /// True if cancellation condition has been met.
        /// False if cancellation request is absent.
        /// </returns>
        private bool DetectCancellation(byte[] recv)
        {
            if (NumCancellationBytesRequired > 0)
            {
                int foundIndex = Array.IndexOf(recv, CAN);
                if (foundIndex > -1)
                {
                    for (int indexToCheck = foundIndex; indexToCheck < recv.Length; indexToCheck++)
                    {
                        // If more than 1 CAN byte is required for cancellation, they must be consecutive
                        // for cancellation to occur.
                        if (recv[indexToCheck] == CAN)
                            _NumCancellationBytesReceived += 1;
                        else
                            _NumCancellationBytesReceived = 0;

                        if (_NumCancellationBytesReceived >= NumCancellationBytesRequired)
                        {
                            Abort();
                            _TerminationReason = TerminationReasonEnum.CancelNotificationReceived;
                            return true;    // Cancellation detected
                        }
                    }
                }
                else
                {
                    _NumCancellationBytesReceived = 0;
                }
            }
            return false;   // No cancellation detected
        }

        private bool EndOfFileAcknowledgementReceived = false;

        /// <summary>
        /// Informs the Receiver that the transmitted file is complete.
        /// Any pending packets are sent, followed by the end-of-file byte.
        /// </summary>
        /// <returns>
        /// The number of user data bytes successfully transmitted during this method call.
        /// </returns>
        public int EndFile()
        {
            // Check if there are unsent data bytes remaining in a pending packet, and if so, send the packet
            // containing the unsent bytes
            if (NumUserDataBytesAddedToCurrentPacket > 0)
            {
                TransmitPacket();
                _TotalUserDataBytesSent += NumUserDataBytesAddedToCurrentPacket;
            }

            CurrentState = States.SenderAwaitingEndOfFileConfirmation;

            int numEndOfFileBytesSent = 0;
            while (EndOfFileAcknowledgementReceived == false && numEndOfFileBytesSent <= MaxSenderRetries)
            {
                WaitForResponseFromReceiver.Reset();
                Port.Write(new byte[] { EndOfFileByteToSend }, 0, 1);
                numEndOfFileBytesSent += 1;
                WaitForResponseFromReceiver.WaitOne();
            }

            if (EndOfFileAcknowledgementReceived == true)
            {
                Abort();
                _TerminationReason = TerminationReasonEnum.EndOfFile;
                return NumUserDataBytesAddedToCurrentPacket;
            }
            else
            {
                Abort();
                _TerminationReason = TerminationReasonEnum.TooManyRetries;
                return 0;
            }
        }

        private void SenderPacketResponseWatchdogRoutine(object notUsed)
        {
            // <ACK>, <NAK> or <CAN> is expected from the Receiver after each packet sent.
            // If an appropriate response is not received within the expected timeout, release the WaitHandle which is enforcing
            // the wait-for-response. This will make the Sender resend the packet once more (until the retry limit is reached).
            if (CurrentState == States.SenderPacketSent)
                WaitForResponseFromReceiver.Set();
        }

        private void ResetSenderPacketResponseWatchdog()
        {
            if (SenderPacketResponseWatchdog != null)
                SenderPacketResponseWatchdog.Change(SenderPacketRetryTimeoutMillisec, SenderPacketRetryTimeoutMillisec);
        }

        private void ReceiverStillAliveWatchdogRoutine(object notUsed)
        {
            Abort();
            _TerminationReason = TerminationReasonEnum.NoResponseFromReceiver;
        }

        private void ResetReceiverStillAliveWatchdog()
        {
            if (ReceiverStillAliveWatchdog != null)
                ReceiverStillAliveWatchdog.Change(SendInactivityTimeoutMillisec, SendInactivityTimeoutMillisec);
        }

        /// <summary>
        /// Removes one or more padding bytes that may exist at the end of a byte array.
        /// </summary>
        /// <param name="input">
        /// The byte array to trim.
        /// </param>
        /// <param name="paddingByteToRemove">
        /// The byte value which defines a padding byte.
        /// If omitted, this defaults to SUB (byte decimal 26).
        /// </param>
        /// <returns>
        /// A byte array without trailing padding bytes (if any are found).
        /// </returns>
        public byte[] TrimPaddingBytesFromEnd(byte[] input, byte paddingByteToRemove = 26)
        {
            int numBytesToDiscard = 0;

            for (int k = input.Length - 1; k >= 0; k--)
            {
                if (input[k] == paddingByteToRemove)
                    numBytesToDiscard += 1;
                else
                    break;
            }

            int numBytesToKeep = input.Length - numBytesToDiscard;

            byte[] output = new byte[numBytesToKeep];
            Array.Copy(input, output, numBytesToKeep);

            return output;
        }

        /// <summary>
        /// Concatenates two byte arrays and returns the combined array.
        /// </summary>
        /// <param name="Array1">The 1st byte array to concatenate.</param>
        /// <param name="Array2">The 2nd byte array to concatenate.</param>
        /// <returns>
        /// The combined byte array which consists of { Array1, Array2 }.
        /// </returns>
        private byte[] CombineArrays(byte[] Array1, byte[] Array2)
        {
            int length1 = Array1.Length;
            int length2 = Array2.Length;

            byte[] combinedArray = new byte[length1 + length2];
            Array1.CopyTo(combinedArray, 0);
            Array2.CopyTo(combinedArray, length1);

            return combinedArray;
        }

    } // End class

    /// <summary>
    /// Calculates CRC-16 CCITT checksum using polynomial (X^16 + X^12 + X^5 + 1).
    /// </summary>
    public class CRC16CCITT
    {
        const ushort poly = 4129;
        ushort[] table = new ushort[256];
        ushort initialValue = 0;

        public ushort ComputeChecksum(byte[] bytes)
        {
            ushort crc = this.initialValue;
            for (int i = 0; i < bytes.Length; ++i)
            {
                crc = (ushort)((crc << 8) ^ table[((crc >> 8) ^ (0xff & bytes[i]))]);
            }
            return crc;
        }

        public byte[] ComputeChecksumBytes(byte[] bytes)
        {
            ushort crc = ComputeChecksum(bytes);
            return BitConverter.GetBytes(crc);
        }

        public enum InitialCrcValue { Zeros, NonZero1 = 0xffff, NonZero2 = 0x1D0F }

        public CRC16CCITT(InitialCrcValue initialValue)
        {
            this.initialValue = (ushort)initialValue;
            ushort temp, a;
            for (int i = 0; i < table.Length; ++i)
            {
                temp = 0;
                a = (ushort)(i << 8);
                for (int j = 0; j < 8; ++j)
                {
                    if (((temp ^ a) & 0x8000) != 0)
                    {
                        temp = (ushort)((temp << 1) ^ poly);
                    }
                    else
                    {
                        temp <<= 1;
                    }
                    a <<= 1;
                }
                table[i] = temp;
            }
        }
    }

} // End namespace