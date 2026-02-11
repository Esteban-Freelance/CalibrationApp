import sys
import telnetlib
import time


class Calib:
    """ Manages communication with Meatest 9010 via ethernet using Telnet protocol. Includes __main__ function with editable test procedure at the end of the script for test purposes. Note 1: 9010 will automatically disconnect after 10 minutes of inactivity. Note 2: All communication gets echoed to stdout. """

    # Communication timeouts
    LOW_TO = 16
    MED_TO = LOW_TO
    HIG_TO = LOW_TO
        
    def __init__(self, calibIPaddr, tnPort=23):
        self.IPaddr = calibIPaddr
        self.tnPort = tnPort
        if not self.openConnection():
            raise Exception("device unreachable")
        try:
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except EOFError as e:
            print(e.args)
            print("cannot establish Telnet protocol")
        self.initialized = True

    def openConnection(self):
        """ initializes Telnet connection and sets up open socket handler """
        try:
            self.tn = telnetlib.Telnet(self.IPaddr, self.tnPort, timeout=5)
            self.connHandshake()
            self.sock = self.tn.get_socket()
            return True
        except:
            return False
    
    def connHandshake(self):
        """ Telnet initialization code block """
        self.writeRawSock(telnetlib.IAC + telnetlib.WILL + telnetlib.NAWS)
        self.writeRawSock(telnetlib.IAC + telnetlib.WILL + telnetlib.TSPEED)
        self.writeRawSock(telnetlib.IAC + telnetlib.WILL + telnetlib.TTYPE)
        self.writeRawSock(telnetlib.IAC + telnetlib.WILL + telnetlib.NEW_ENVIRON)
        self.writeRawSock(telnetlib.IAC + telnetlib.DO   + telnetlib.ECHO)
        self.writeRawSock(telnetlib.IAC + telnetlib.WILL + telnetlib.SGA)
        self.writeRawSock(telnetlib.IAC + telnetlib.DO   + telnetlib.SGA)

    def writeRawSock(self, seq):
        """ sends seq byte sequence to device over Telnet """
        sock = self.tn.get_socket()
        if sock is not None:
            sock.send(seq)

    def clearStatus(self):
        """ Clears device status byte and error queue """
        scpiCmd = b'*CLS\r\n'
        self.tn.write(scpiCmd)
        #self.tn.write(b'\r\n')
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        return

    def waitCmdComplete(self):
        """ Sends *WAI: Prevents the device from processing further commands but DOES NOT HALT the PC program """
        scpiCmd = b'*WAI\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.HIG_TO)

    def isReady(self, opcTO):
        """ Sends *OPC?: Prevents the device from processing further commands and HALTS the PC program """
        scpiCmd = b'*OPC?\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'1\r\n', timeout=opcTO)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)

    def reset(self):
        """ Sends *RST to set the device to default state """
        scpiCmd = b'*RST\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.HIG_TO)
    
    def enableRemote(self):
        """ Sends SYST:REM to switch the device into Remote Mode (9010 will respond only to *IDN? otherwise)"""
        scpiCmd = b'SYST:REM\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.MED_TO)

    def readErr(self):
        """ Sends SYST:ERR?: Reads device's error queue """
        error = ''
        errorList = []
        scpiCmd = b'SYST:ERR?\r\n'
        self.tn.write(scpiCmd)
        try:
            error = self.tn.read_until(b'\r\n', timeout=Calib.MED_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(error) > len(scpiCmd.strip()): # there is echo
            error = error.strip().decode('ASCII') # remove CR LF and convert to ASCII
            error = error[len(scpiCmd.strip()):] # remove the command from the answer because the device echoes
            errorList.append(error)
            try:
                errorNum = int(error.split(',')[0]) # get the error count
            except:
                if len(errorList) > 0: # unexpected string received
                    errorList.pop()
            if errorNum == 0: # no errors left
                return errorList
            else:
                errorList.extend(self.readErr()) # read the other errors
        return errorList

    def identity(self):
        """ Sends *IDN?: Reads device info inc. Make, Model, SN, FW version """
        idn = ''
        scpiCmd = b'*IDN?\r\n'
        self.tn.write(scpiCmd) #ConnectionAbortedError nel caso di connessione chiusa
        try:
            idn = self.tn.read_until(b'\r\n', timeout=Calib.LOW_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(idn) > len(scpiCmd.strip()):
            idn = idn.strip().decode('ASCII') # remove CR LF and convert to ASCII
            idn = idn[len(scpiCmd.strip()):] # remove the command from the answer because the device echoes
        return idn

    def getOutputState(self):
        """ Sends OUTP:STAT?: Reads output state (on/off) """
        state = b''
        scpiCmd = b'OUTP:STAT?\r\n'
        self.tn.write(scpiCmd)
        try:
            state = self.tn.read_until(b'\r\n', timeout=Calib.LOW_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(state) > len(scpiCmd.strip()):
            state = state.strip().decode('ASCII')
            state = state[len(scpiCmd.strip()):]
        return state

    def outputOn(self):
        """ Sends OUTP:STAT 1 to turn the output on """
        scpiCmd = b'OUTP:STAT 1\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)

    def outputOff(self):
        """ Sends OUTP:STAT 0 to turn the output off """
        scpiCmd = b'OUTP:STAT 0\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        
    def getVoltage(self):
        """ Reads set output DC voltage """
        vdc = b''
        scpiCmd = b'VOLT?\r\n'
        self.tn.write(scpiCmd)
        try:
            vdc = self.tn.read_until(b'\r\n', timeout=Calib.LOW_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(vdc) > len(scpiCmd.strip()):
            vdc = vdc.strip().decode('ASCII')
            vdc = vdc[len(scpiCmd.strip()):]
        return vdc

    def setVoltage(self, vdc):
        """ Sets output DC voltage """
        scpiCmd = b'VOLT ' + str(vdc).encode('UTF-8') + b'\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        
    def getVoltageRange(self):
        """ Reads active DC voltage range """
        vdcRange = b''
        scpiCmd = b'VOLT:RANG?\r\n'
        self.tn.write(scpiCmd)
        try:
            vdcRange = self.tn.read_until(b'\r\n', timeout=Calib.LOW_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(vdcRange) > len(scpiCmd.strip()):
            vdcRange = vdcRange.strip().decode('ASCII')
            vdcRange = vdcRange[len(scpiCmd.strip()):]
        return vdcRange

    def setVoltageRange(self, range):
        """ Sets DC voltage range. Available options are 0.02|0.2|2|20|100|280|1000|AUTO """
        if range == 'AUTO':
            scpiCmd = b'VOLT:RANG:AUTO ON\r\n'
        else:
            scpiCmd = b'VOLT:RANG ' + str(range).encode('UTF-8') + b'\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        
    def getOutputGndState(self):
        """ Reads LO terminal grounding state for Voltage functions: "FLO" means floating, "GRO" means grounded """
        state = b''
        scpiCmd = b'OUTP:LOW1?\r\n'
        self.tn.write(scpiCmd)
        try:
            state = self.tn.read_until(b'\r\n', timeout=Calib.LOW_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(state) > len(scpiCmd.strip()):
            state = state.strip().decode('ASCII')
            state = state[len(scpiCmd.strip()):]
        return state

    def setOutputGndFloating(self):
        """ Sends OUTP:LOW1 FLO to disconnect LO terminal from ground in Voltage functions """
        scpiCmd = b'OUTP:LOW1 FLO\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)

    def setOutputGndToEarth(self):
        """ Sends OUTP:LOW1 GRO to connect LO terminal to ground in Voltage functions """
        scpiCmd = b'OUTP:LOW1 GRO\r\n'
        self.tn.write(scpiCmd)
        self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)

    def sendCommand(self, cmd):
        """ Sends a command defined in cmd """
        resp = b''
        scpiCmd = cmd.encode('UTF-8') + b'\r\n'
        self.tn.write(scpiCmd)
        try:
            resp = self.tn.read_until(b'\r\n', timeout=Calib.LOW_TO)
            self.tn.read_until(b'9010> ', timeout=Calib.LOW_TO)
        except:
            pass
        if len(resp) > len(scpiCmd.strip()):
            resp = resp.strip().decode('ASCII')
            resp = resp[len(scpiCmd.strip()):]
        return resp
        
    def close(self):
        self.tn.close()

if __name__ == "__main__":
	# connection init code block
    IPaddr = '192.168.1.100' # SET IPv4 ADDRESS OF THE 9010 HERE
    calibrator = Calib(IPaddr)
    calibrator.enableRemote()
    calibrator.reset()
    calibrator.isReady(0.5)
    calibrator.clearStatus()

    # TEST PROCEDURE CODE BLOCK - FEEL FREE TO EDIT
    # This procedure prints device info, steps through voltages 1 V, 2 V and 3 V and prints errors along the way (if any)
    print(calibrator.identity())
    calibrator.setVoltageRange('AUTO')
    for V in range(5,10):
        calibrator.setVoltage(V)
        calibrator.outputOn()
        time.sleep(2)
        calibrator.outputOff()
        s=calibrator.readErr()
        print("error =", s)

	# connection termination code block
    calibrator.close()
