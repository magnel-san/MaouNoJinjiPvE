using UnityEngine;
using UnityEngine.UI;

public class WebCamController : MonoBehaviour
{
    [SerializeField] private RawImage displayImage; // 映像を表示するRawImage
    private WebCamTexture webCamTexture;

    void Start()
    {
        // 接続されているすべてのカメラデバイスを取得
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("カメラが見つかりません。");
            return;
        }

        string targetDeviceName = "";

        // 接続されているカメラの名前をログで確認
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"カメラ index {i}: {devices[i].name}");

            // 例: 特定のWebカメラ名が含まれているか判定して選択
            // ※ 使用したい外付けカメラの名称の一部（例: "Logitech", "USB", "HD Pro" など）を指定
            if (devices[i].name.Contains("USB") || !devices[i].isFrontFacing)
            {
                targetDeviceName = devices[i].name;
            }
        }

        // 条件に合うカメラが見つからなければリストの最後のカメラ（外付けのことが多い）を使用
        if (string.IsNullOrEmpty(targetDeviceName))
        {
            targetDeviceName = devices[devices.Length - 1].name;
        }

        // デバイス名を指定してWebCamTextureを作成 (幅, 高さ, FPS)
        webCamTexture = new WebCamTexture(targetDeviceName, 1280, 720, 30);

        // RawImageにカメラ映像をセットして再生
        if (displayImage != null)
        {
            displayImage.texture = webCamTexture;
            webCamTexture.Play();
        }
    }

    void OnDestroy()
    {
        // メモリリーク防止のためシーン終了時に停止
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
        }
    }
}
