# Python 3 web server for MonsterShield
from http.server import BaseHTTPRequestHandler, HTTPServer
import time
import ssl
import os
#from gpiozero import LED
import serial

hostName = ""
"""hostName = "localhost" """
"""serverPort = 8080"""
serverPort = 4443
# note:  If you want to use TLS (HTTPS), then you will need to have a SSL
# cert (take a look at openssl to generate your own), and uncomment the
# socket lines at the end of the code and specify your cert files.

ser = None


enginespeed = 0.5
curspeed = 0.0

def connectToMonster():
    try:
        ser = serial.Serial('/dev/ttyACM0',115200)
    except:
        print("Can't Connect!")


def ReadData():
    if not ser is None:
        bytesToRead = ser.inWaiting()
        print(ser.read(bytesToRead))


time.sleep(5)
ReadData()

class MyServer(BaseHTTPRequestHandler):

    def do_GET(self):

        if ser is None:
            connectToMonster()

        if self.path == "/":

            root = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'html')
            filename = 'indexmonster.html'
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.end_headers()

            with open(filename, 'rb') as fh:
                html = fh.read()
                #html = bytes(html, 'utf8')
                self.wfile.write(html)

        if self.path == "/s0":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #0", "utf-8"))
            if not ser is None:
                ser.write(b'@T0')

        if self.path == "/s1":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #1", "utf-8"))
            if not ser is None:
                ser.write(b'@T1')

        if self.path == "/s2":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #2", "utf-8"))
            if not ser is None:
                ser.write(b'@T2')

        if self.path == "/s3":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #3", "utf-8"))
            if not ser is None:
                ser.write(b'@T3')  

        if self.path == "/s4":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #4", "utf-8"))
            if not ser is None:
                ser.write(b'@T4') 

        if self.path == "/s5":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #5", "utf-8"))
            if not ser is None:
                ser.write(b'@T5')

        if self.path == "/s6":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #6", "utf-8"))
            if not ser is None:
                ser.write(b'@T6')  


        if self.path == "/s7":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #7", "utf-8"))
            if not ser is None:
                ser.write(b'@7')  


        if self.path == "/s8":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #8", "utf-8"))
            if not ser is None:
                ser.write(b'@T8')


        if self.path == "/s9":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #9", "utf-8"))
            if not ser is None:
                ser.write(b'@T9')


        if self.path == "/sa":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #A", "utf-8"))
            if not ser is None:
                ser.write(b'@Ta')


        if self.path == "/sb":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #B", "utf-8"))
            if not ser is None:
                ser.write(b'@Tb')


        if self.path == "/sc":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #C", "utf-8"))
            if not ser is None:
                ser.write(b'@Tc')

                      
        if self.path == "/sd":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #D", "utf-8"))
            if not ser is None:
                ser.write(b'@Td')


        if self.path == "/se":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #E", "utf-8"))
            if not ser is None:
                ser.write(b'@Te')


        if self.path == "/sf":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Trigger #F", "utf-8"))
            if not ser is None:
                ser.write(b'@Tf')
           

        if self.path == "/triggerson":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Triggers ON", "utf-8"))
            if not ser is None:
                ser.write(b'@i')

        if self.path == "/triggersoff":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Triggers OFF", "utf-8"))
            if not ser is None:
                ser.write(b'@I')                       

        if self.path == "/stop":
            self.send_response(200)
            self.send_header("Content-type", "text/plain")
            self.end_headers()            
            self.wfile.write(bytes("Execute STOP", "utf-8"))
            if not ser is None:
                ser.write(b'@*')
         


        ReadData()
        time.sleep(2)
        ReadData()


if __name__ == "__main__":        
    webServer = HTTPServer((hostName, serverPort), MyServer)

#### UNCOMMENT the 4 lines below if you are using SSL, and supply your key.pem and cert.pem file names.
#    webServer.socket = ssl.wrap_socket (webServer.socket, 
#        keyfile="key.pem",
#        certfile="cert.pem",
#        server_side=True)

    print("Server started http://%s:%s" % (hostName, serverPort))

    try:
        webServer.serve_forever()
    except KeyboardInterrupt:
        pass

    webServer.server_close()
    print("Server stopped.")