以下のコードを Google Apps Script で、  
ウェブアプリとして実行できるようにする。  

https://developers.google.com/apps-script?hl=ja



```
function doGet( e )
{
  return translate( e ) ;
}

function doPost( e )
{
  return translate( e ) ;
}

function translate( e )
{
    // リクエストパラメータを取得する
    var p = e.parameter ;

    //  LanguageAppクラスを用いて翻訳を実行
    var translatedText = LanguageApp.translate( p.text, p.source, p.target ) ;

    // レスポンスボディの作成
    var body ;
    if( translatedText )
    {
        body =
        {
          code: 200,
          text: translatedText
        } ;
    }
    else
    {
        body =
        {
          code: 400,
          text: "Bad Request"
        } ;
    }

    // レスポンスの作成
    var response = ContentService.createTextOutput() ;

    // Mime TypeをJSONに設定
    response.setMimeType( ContentService.MimeType.JSON ) ;

    // JSONテキストをセットする
    response.setContent( JSON.stringify( body ) ) ;

    return response ;
}
```

上記のコードによるウェブアプリのURLを取得して、  
Program.cs の m_TranslationApi に設定する。

