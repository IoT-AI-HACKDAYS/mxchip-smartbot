#include "azure_config.h"

#include "AZ3166WiFi.h"
#include "http_client.h"
#include "Arduino.h"
#include "OledDisplay.h"
#include "AudioClassV2.h"

AudioClass &Audio = AudioClass::getInstance();
const int AUDIO_SIZE = 32000 * 2 + 45;

char *audioBuffer;
int monoSize;

bool hasWifi = false;

void setup(void)
{

  pinMode(LED_BUILTIN, OUTPUT);

  //Initialise and connect to the WIFI
  InitWiFi();

  Audio.format(16000, 16);

  Serial.begin(115200);
  Serial.println("Hello to the IoT Hackdays sample");

  //allocate buffer space
  audioBuffer = (char *)malloc(AUDIO_SIZE + 1);
  memset(audioBuffer, 0x0, AUDIO_SIZE);
}

void InitWiFi()
{
  if (WiFi.begin() == WL_CONNECTED)
  {
    IPAddress ip = WiFi.localIP();
    Screen.print(1, ip.get_address());
    Serial.printf("Local device ip : %s\r\n", ip.get_address());
    hasWifi = true;
  }
  else
  {
    Screen.print(1, "No Wi-Fi");
    Serial.println("Could not connect to WIFI!");
  }
}

void loop(void)
{
  if (hasWifi)
  {
    listen();
    const char *intent = resolve();
    if (intent != NULL)
    {
      commandBot(intent);
      Screen.print(1, intent);
    }
  }
  delay(100);
}

void listen()
{
  Screen.clean();
  Screen.print(0, "Listening...");
  Serial.println("listening");

  // Start to record audio data
  Audio.startRecord(audioBuffer, AUDIO_SIZE);

  // Check whether the audio record is completed.
  Serial.printf("Audio State %d\r\n", Audio.getAudioState());
  while (Audio.getAudioState() == AUDIO_STATE_RECORDING)
  {
    //check to see if the buffer is "full"
    int currentBufferSize = Audio.getCurrentSize();

    if (currentBufferSize >= AUDIO_SIZE - 1)
    {
      Audio.stop();
      monoSize = Audio.convertToMono(audioBuffer, currentBufferSize, 16);
    }
    delay(10);
  }
  Screen.clean();
}

const char *resolve()
{
  Screen.clean();
  Screen.print(2, "uploading..please wait..");
  Serial.println("Uploading captured buffer");

  HTTPClient *httpClient = new HTTPClient(HTTP_POST, AZURE_FUNCTION_URL);
  const Http_Response *result = httpClient->send(audioBuffer, monoSize);
  if (result == NULL)
  {
    Screen.print(1, "Failed");
    Serial.print("Failed to submit recoridng \r\nError Code: ");
    Serial.println(httpClient->get_error());
    return NULL;
  }
  else
  {
    Screen.print(1, "Successfull");

    Serial.println("Response from function:");
    Serial.printf("\tStatus %s \r\n", result->status_message);
    const char *body = result->body;
    Serial.printf("\tBody %s \r\n", body);
    Screen.clean();
    Screen.print(0, "Found intent:");
    Screen.print(1, body);

    return body;
  }
}

void commandBot(const char *command)
{
  if (command == "Bot.Stop")
  {
    Screen.print(3, "stopping..");
  }
  else if (command == "Bot.Reverse")
  {
    Screen.print(3, "backing up..");
  }
  else if (command == "Bot.MoveFroward")
  {
    Screen.print(3, "make way.. coming through..");
  }
   else if (command == "Bot.MoveLeft")
  {
    Screen.print(3, "turn left");
  }
   else if (command == "Bot.MoveRight")
  {
    Screen.print(3, "turn right");
  }
 
}