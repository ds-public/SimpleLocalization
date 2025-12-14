//#define USE_OPEN_XML

// null 許容を有効化しワーニングを抑制する
#nullable enable
#pragma warning disable CA1858
#pragma warning disable CA1862
#pragma warning disable CA2211
#pragma warning disable CS8600
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8618
#pragma warning disable CS8619
#pragma warning disable CS8625
#pragma warning disable IDE0059
#pragma warning disable IDE0063
#pragma warning disable IDE0075
#pragma warning disable IDE0300
#pragma warning disable IDE0305
#pragma warning disable IDE0130
#pragma warning disable IDE1006


using System ;
using System.Collections.Generic ;
using System.Text ;
using System.Text.Json ;
using System.IO ;
using System.Runtime ;
using System.Net ; // WebUtilityを使用するために必要
using System.Net.Http ;
using System.Threading.Tasks ;
using System.Linq ;

using System.Diagnostics ;

using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel ;


using CC=System.ConsoleColor ;


namespace SimpleLocalization
{
	public static class Program
	{
		// Google 翻訳 API の URL
		private const string m_TranslationApi = "https://script.google.com/macros/s/AKfycbzln3vdk_a4RplLodsajbzidKK0YUT9NLdZ4tkpHuULNw-JArM2w4Vdja9K3Sv1FYPskw/exec" ;

		/// <summary>
		/// メイン
		/// </summary>
		/// <param name="args"></param>
		/// <returns></returns>
		public static async Task Main( params string[] args )
		{
			// カレントディレクトリ
			string currentDirectory = FilePath.GetCurrentDirectory().Replace( "\n", "/" ) ;

			//------------------------------------------------------------------------------------------

			// コマンドラインへのパラメータを分解する

			string	target_BookPath			= string.Empty ;	// ブック名
			string	target_SheetName		= string.Empty ;	// シート名

			int		column_OriginalText		= 0 ;				// バリュー(翻訳前)のカラム
			int		column_TranslatedText	= 1 ;				// バリュー(翻訳後)のカラム

			string	language_OriginalText	= "en" ;			// 翻訳前の言語
			string	language_TranslatedText	= "ja" ;			// 翻訳後の言語

			// コマンドラインパラメータの分解
			if( args != null && args.Length >  0 )
			{
				int i, l = args.Length ;

				for( i  = 0 ; i <  l ; i ++ )
				{
					string argv = args[ i ] ;

					if( argv.StartsWith( "-TB=", StringComparison.OrdinalIgnoreCase ) == true )
					{
						// 対象ファイル名
						target_BookPath		= argv[ 4.. ] ;
					}
					else
					if( argv.StartsWith( "-TS=", StringComparison.OrdinalIgnoreCase ) == true )
					{
						// 対象シート名
						target_SheetName	= argv[ 4.. ] ;
					}
					else
					if( argv.StartsWith( "-CO=", StringComparison.OrdinalIgnoreCase ) == true )
					{
						// 翻訳前のテキストのカラム位置
						string c = argv[ 4.. ] ;
						if( c.Length == 1 )
						{
							if( c[ 0 ] >= 'a' && c[ 0 ] <= 'z' )
							{
								column_OriginalText = c[ 0 ] - 'a' ;
							}
							else
							if( c[ 0 ] >= 'A' && c[ 0 ] <= 'Z' )
							{
								column_OriginalText = c[ 0 ] - 'A' ;
							}
						}
					}
					else
					if( argv.StartsWith( "-CT=", StringComparison.OrdinalIgnoreCase ) == true )
					{
						// 翻訳後のテキストのカラム位置
						string c = argv[ 4.. ] ;
						if( c.Length == 1 )
						{
							if( c[ 0 ] >= 'a' && c[ 0 ] <= 'z' )
							{
								column_TranslatedText = c[ 0 ] - 'a' ;
							}
							else
							if( c[ 0 ] >= 'A' && c[ 0 ] <= 'Z' )
							{
								column_TranslatedText = c[ 0 ] - 'A' ;
							}
						}
					}
					else
					if( argv.StartsWith( "-LO=", StringComparison.OrdinalIgnoreCase ) == true )
					{
						// 翻訳前の言語コード
						language_OriginalText = argv[ 4.. ] ;
					}
					else
					if( argv.StartsWith( "-LT=", StringComparison.OrdinalIgnoreCase ) == true )
					{
						// 翻訳後の言語コード
						language_TranslatedText = argv[ 4.. ] ;
					}
				}
			}
			else
			{
				// コマンドラインパラメータが無い場合はヘルプを表示する

				string packageName = typeof( Program ).ToString().Replace( ".Program", "" ) ;

				int nameIndex = packageName.LastIndexOf( '.' ) ;
				string applicationName = packageName[ ( nameIndex + 1 ).. ] ;

				string help = applicationName + " [Options]" + "\n" +
					" Options:" + "\n" +
					"  -TB=対象ファイル名(または絶対パス)" + "\n" +
					"  -TS=対象シート名" + "\n" +
					"  -CO=翻訳前カラム(A,B,C...)" + "\n" +
					"  -CT=翻訳後カラム(A,B,C...)" + "\n" +
					"  -LO=翻訳前の言語(en,ja,kr,cn...)" + "\n" +
					"  -LT=翻訳後の言語(en,ja,kr,cn...)" + "\n" +
					" Exsample:" + "\n" +
					"  >" + applicationName + " -TF=sample.xlsx -TS=sample -CO=A -CT=B -LO=en -LT=ja" ;

				Print( help, CC.Yellow ) ;

				return ;
			}

			//------------------------------------------------------------------------------------------

			if( string.IsNullOrEmpty( target_BookPath ) == true )
			{
				Print( "対象ブック名が指定されていません", CC.Red ) ;
				return ;
			}

			if( string.IsNullOrEmpty( target_SheetName ) == true )
			{
				// シート名が省略されている場合はブック名からシート名を設定する
				target_SheetName = Path.GetFileNameWithoutExtension( target_BookPath ) ;
			}

			if( string.IsNullOrEmpty( language_OriginalText ) == true || string.IsNullOrEmpty( language_TranslatedText ) == true )
			{
				Print( "翻訳前または翻訳後の言語コードが指定されていません", CC.Red ) ;
				return ;
			}

			//------------------------------------------------------------------------------------------

			if( target_BookPath.Contains( ':' ) == false )
			{
				// 相対パスとみなす→絶対パスにする
				target_BookPath = FilePath.GetMergedPath( currentDirectory, target_BookPath ) ;
			}

			//------------------------------------------------------------------------------------------

			Print( "対象ブック名 : " + target_BookPath ) ;
			Print( "対象シート名 : " + target_SheetName ) ;

			Print( "翻訳前の言語コード : " + language_OriginalText, CC.Yellow ) ;
			Print( "翻訳後の言語コード : " + language_TranslatedText, CC.Yellow ) ;

			if( File.Exists( target_BookPath ) == true )
			{
				Console.WriteLine( "===========================" ) ;

				// 翻訳を実行する
				await TranslateAsync
				(
					target_BookPath,
					target_SheetName,
					column_OriginalText,
					column_TranslatedText,
					language_OriginalText,
					language_TranslatedText
				) ;
			}
			else
			{
				Print( "ブックが存在しません : " + target_BookPath, CC.Red ) ;
			}
		}

		//-------------------------------------------------------------------------------------------

		/// <summary>
		/// 翻訳APIからの応答(Jsonをデシリアライズ)
		/// </summary>
		public class ResponseData
		{
			public int		code	{ get ; set ; }
			public string	text	{ get ; set ; }
		}

		// １件の翻訳のタイムアウト
		private const float m_TimeOutTime = 10.0f ;

		//-------------------------------------------------------------------------------------------

		// 翻訳を実行する
		private static async Task TranslateAsync
		(
			string target_BookPath,
			string target_SheetName,
			int column_OriginalText,
			int column_TranslatedText,
			string language_OriginalText,
			string language_TranslatedText
		)
		{
			Print( "<<< Translation Started >>>" ) ;

			long unit = 10000 ;	// 処理時間をミリ秒で表示するための基本単位

			long baseTicks = DateTime.Now.Ticks ;	// 処理時間計測用

			//----------------------------------------------------------

			// 翻訳クラスのインスタンスを生成する
			var ta = new TranslatorAccessor( m_TimeOutTime ) ;

			// 翻訳のリクエストパラメータ
			var parameters = new Dictionary<string,string>()
			{
				{ "text", "" },
				{ "source", language_OriginalText },	// 翻訳前の言語コード
				{ "target", language_TranslatedText },	// 翻訳後の言語コード
			} ;

			//----------------------------------

			int translatedCountNow = 0 ;	// 翻訳成功数
			int translatedCountMax = 0 ;	// 翻訳実行数

			//------------------------------------------------------------------------------------------
			// 対象翻訳

			XSSFWorkbook workBook = null ;

			try
			{
				using( var fs = new FileStream( target_BookPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) )
				{
					// .xlsx 形式に対応する XSSFWorkbook を使用してワークブックを読み込み
					workBook = new XSSFWorkbook( fs ) ;

					ISheet sheet = null ;

					if( string.IsNullOrEmpty( target_SheetName ) == true )
					{
						sheet = workBook.GetSheetAt( 0 ) ;
					}
					else
					{
						sheet = workBook.GetSheet( target_SheetName ) ;
					}

					if( sheet == null )
					{
						Print( $"シートが開けません : {target_SheetName}", CC.Red ) ;
						return ;
					}

					//------------------------------------------------------------------------

					uint firstRowIndex = ( uint )sheet.FirstRowNum ;
					uint lastRowIndex  = ( uint )sheet.LastRowNum ;

					Print( "処理開始行 : " + firstRowIndex ) ;
					Print( "処理終了行 : " + lastRowIndex ) ;
					Print( "===========================" ) ;

					for( uint rowIndex = firstRowIndex ; rowIndex <= lastRowIndex ; rowIndex ++ )
					{
						// 行の情報を取得する
						IRow row = sheet.GetRow( ( int )rowIndex ) ;

						// 行が存在しない場合はスキップ（空白行など）
						if( row == null )
						{
							continue ;
						}

						//-------------------------------

						// 翻訳前のテキストを取得する
						string value = GetCellValueAsString( row, ( uint )column_OriginalText ) ;

						if( string.IsNullOrEmpty( value ) == false )
						{
							// 変換を実行する

							// 翻訳実行数
							translatedCountMax ++ ;

							Print( "-------> 行 : " + string.Format( "{0,6}", ( 1 + rowIndex ) ) ) ;
							Print( value, CC.Yellow ) ;
							Print( "↓(翻訳中…)", CC.Magenta ) ;

							// 参考 : https://script.google.com/home

							// 翻訳対象のテキストをリクエストパラメータに設定
							parameters[ "text" ] = value ;

							// 翻訳を実行する(GETはQueryString2048文字制限があるのでPOST使用を推奨)
//							string responseString = await ta.GetAsync( m_TranslateApi, parameters ) ;	// こちらも一応使える
							string responseString = await ta.PostAsync( m_TranslationApi, parameters ) ;

							if( string.IsNullOrEmpty( responseString ) == false )
							{
								// レスポンスを展開(デシリアライズ)する
								var response = JsonSerializer.Deserialize<ResponseData>( responseString ) ;

								if( response != null && response.code == 200 )
								{
									// 翻訳成功

									//---------------------------

									// セルの値を設定する
									SetCellValue( row, ( uint )column_TranslatedText, response.text ) ;

									// 翻訳成功数
									translatedCountNow ++ ;

									//---------------------------
									// ログ

									Print( response.text, CC.Cyan ) ;
								}
								else
								{
									Print( "翻訳失敗", CC.Red ) ;
								}
							}
							else
							{
								Print( "翻訳失敗", CC.Red ) ;
							}
						}
					}
				}
			}
			catch( Exception e )
			{
				Print( $"予期せぬエラーが発生しました[2]: {e.Message}", CC.Red ) ;
			}

			//---------------------------------------------------------
			// 書き込み

			// ※ブックファイルを開いていると上書保存できないため別ブックファイルとして保存する

			if( workBook != null && translatedCountNow >  0 )
			{
				// １件でも翻訳が行われていれば保存

				try
				{
					using( var fs = new FileStream( target_BookPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite ) )
					{
						Print( "=============================================" ) ;
						Print( ">>> 書き込み実行" ) ;

						workBook.Write( fs ) ;
					}
					// 上書保存に成功
				}
				catch( Exception e )
				{
					// ブックファイルが開かれていて上書きできない(本当は例外を厳密にチェックしてた方が良いが省略)

					// 別ブックファイルとして保存する
					var dt = DateTime.Now ;
					string ts = $"{dt.Year:D4}-{dt.Month:D2}-{dt.Day:D2}_{dt.Hour:D2}-{dt.Minute:D2}-{dt.Second:D2}" ;
					string cloned_BookPath = target_BookPath.Replace( ".xlsx", $"_{ts}.xlsx" ) ;

					try
					{
						using( var fs = new FileStream( cloned_BookPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite ) )
						{
							workBook.Write( fs ) ;
						}
						// 別名保存に成功

						Console.WriteLine( "===========================" ) ;
						Print( "処理中のファイルの書き込みに失敗しました。対象のファイルを開いている場合は閉じた状態で実行してください。 : " + e.Message + "\n別名【" + cloned_BookPath + "】で保存しました", CC.Yellow ) ;
					}
					catch( Exception )
					{
						Print( "ファイルの書き込みに失敗しました。対象のファイルを開いている場合は閉じた状態で実行してください。 : " + e.Message, CC.Red ) ;
					}
				}
			}

			//-------------------------------------------------------
			// 破棄

			if( workBook != null )
			{
				workBook.Close() ;
				workBook.Dispose() ;
			}

			//----------------------------------------------------------
			// 翻訳数・翻訳率の表示

			Print( "===========================" ) ;
			Print( "翻訳数 : " + translatedCountNow + " / " + translatedCountMax, CC.Green ) ;

			if( translatedCountMax >  0 )
			{
				double avarage ;
				int avarage_i ;

				avarage = ( double )translatedCountNow / ( double )translatedCountMax ;
				avarage_i = ( int )( avarage * 1000 ) ;

				Print( "翻訳率 " + ( int )( avarage_i / 10 ) + "." + ( avarage_i % 10 ) + "% : 残り = " + ( translatedCountMax - translatedCountNow ) + " 件" , CC.Green ) ;
			}

			//----------------------------------------------------------
			// 処理時間の表示

			long exitTicks = DateTime.Now.Ticks ;

			// 処理時間をミリ秒に変換
			long processingTime = ( exitTicks - baseTicks )  / unit ;

			long processingMinute = processingTime / ( 60 * 1000 ) ;
			processingTime %= ( 60 * 1000 ) ;
			long processingSecond = processingTime /        1000 ;

			Print( $"処理時間 : {processingMinute}分{processingSecond}秒", CC.Magenta ) ;
		}

		//-------------------------------------------------------------------------------------------

		// セルの値を文字列として取得する
		private static string GetCellValueAsString( IRow row, uint cIndex )
		{
			ICell cell = row.GetCell( ( int )cIndex ) ;

			// セルが存在しない場合はスキップ
			if( cell == null )
			{
				return string.Empty ;
			}

			return GetCellValueAsString( cell ) ;
		}

		// セルの値を文字列として取得する
		private static string GetCellValueAsString( ICell cell )
		{
			// NPOIでは、セルの種類（文字列、数値、数式など）に応じて値を取得する必要がある
			switch( cell.CellType )
			{
				case NPOI.SS.UserModel.CellType.String :
					return cell.StringCellValue ;

				case NPOI.SS.UserModel.CellType.Numeric :
					// 数値または日付の場合
					if( DateUtil.IsCellDateFormatted( cell ) )
					{
						// 日付としてフォーマットされている場合
						return cell.DateCellValue?.ToString( "yyyy/MM/dd" ) ;
					}
					else
					{
						// 標準の数値の場合
						return cell.NumericCellValue.ToString() ;
					}

				case NPOI.SS.UserModel.CellType.Boolean :
					return cell.BooleanCellValue.ToString() ;

				case NPOI.SS.UserModel.CellType.Formula :
					// 数式の場合、計算結果の値を返す
					// 数式の評価には IFormulaEvaluator が必要になる場合があるが、
					// 今回は最も簡単な CachedFormulaResultType に基づく値を取得
					// 注意: 正確な評価が必要な場合は別途 IFormulaEvaluator を使用してください
					return cell.CachedFormulaResultType switch
					{
						NPOI.SS.UserModel.CellType.Numeric => cell.NumericCellValue.ToString(),
						NPOI.SS.UserModel.CellType.String => cell.StringCellValue,
						NPOI.SS.UserModel.CellType.Boolean => cell.BooleanCellValue.ToString(),
						_ => cell.ToString()
					} ;

				case NPOI.SS.UserModel.CellType.Blank :
					return string.Empty ;

				default :
					return cell.ToString() ;
			}
		}

		// セルの値を設定する
		private static void SetCellValue( IRow row, uint columnIndex, string value )
		{
			ICell cell = row.GetCell( ( int )columnIndex ) ;
			cell ??= row.CreateCell( ( int )columnIndex ) ;

			cell?.SetCellValue( value ) ;
		}

		//-----------------------------------------------------------

		// コンソールへの色付き文字列の出力
		private static void Print( string message, ConsoleColor color = ConsoleColor.White )
		{
			Console.ForegroundColor = color ;
			Console.WriteLine( message ) ;
			Console.ResetColor() ;
		}


	}	// class

	//--------------------------------------------------------------------------------------------

	/// <summary>
	/// 外部サービスを利用しての翻訳実行を行うクラス
	/// </summary>
	public class TranslatorAccessor
	{
		// HttpClientは、アプリケーションのライフサイクル全体で再利用することが推奨されます。
		// static readonly で宣言することで、再利用を促進します。
		private readonly HttpClient m_Client ;

		/// <summary>
		/// コンストラクタ
		/// </summary>
		/// <param name="timeOutTime"></param>
		public TranslatorAccessor( float timeOutTime )
		{
			m_Client = new HttpClient()
			{
				Timeout = TimeSpan.FromSeconds( timeOutTime ) 
			} ;
		}

		/// <summary>
		/// GET
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public async Task<string> GetAsync( string url, Dictionary<string,string> parameters )
		{
			try
			{
				// 1. クエリ文字列を構築
				string queryString = BuildQueryString( parameters ) ;

				if( string.IsNullOrEmpty( queryString ) == false )
				{
					url = $"{url}?{queryString}" ;
				}

				// GetStringAsyncは、GETリクエストを送り、応答ボディを文字列として返します。
				// 応答ステータスコードが成功（200-299）でない場合は、例外（HttpRequestException）をスローします。
				string responseBody = await m_Client.GetStringAsync( url ) ;
			
				return responseBody ;
			}
			catch( HttpRequestException e )
			{
				// 接続エラーや非成功ステータスコード（4xx, 5xxなど）の場合の処理
				Console.WriteLine( $"\n例外発生: {e.Message}" ) ;
				Console.WriteLine( $"URL: {url}" ) ;
			
				// エラーが発生した場合は、nullまたは空文字列を返すなど、適切なエラー処理を行います。
				return null ; 
			}
			catch( Exception ex )
			{
				// その他の予期せぬ例外の処理
				Console.WriteLine( $"\n予期せぬ例外: {ex.Message}" ) ;
				return null ;
			}
		}

		/// DictionaryをURLエンコードされたクエリ文字列に変換します。
		/// </summary>
		private static string BuildQueryString( Dictionary<string, string> parameters )
		{
			if( parameters == null || parameters.Count == 0 )
			{
				return null ;
			}

			//----------------------------------

			var encodedPairs = new List<string>() ;

			foreach( var pair in parameters )
			{
				// キーと値をそれぞれURLエンコード
				string encodedKey   = WebUtility.UrlEncode( pair.Key ) ;
				string encodedValue = WebUtility.UrlEncode( pair.Value ) ;
			
				// "key=value" の形式でリストに追加
				encodedPairs.Add( $"{encodedKey}={encodedValue}" ) ;
			}

			// 全てのペアを "&" で結合
			return string.Join( "&", encodedPairs ) ;
		}

		/// <summary>
		/// POST
		/// </summary>
		/// <param name="url"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public async Task<string> PostAsync( string url, Dictionary<string,string> parameters )
		{
			try
			{
				// 1. DictionaryをFormUrlEncodedContentとしてラップします。
				//    これにより、データは自動的にURLエンコードされ、
				//    Content-Typeヘッダーが "application/x-www-form-urlencoded" に設定されます。
				var content = new FormUrlEncodedContent( parameters ) ;

				// 2. POSTリクエストを送信
				HttpResponseMessage response = await m_Client.PostAsync( url, content ) ;

				// 3. ステータスコードが成功（200番台）かチェックし、失敗なら例外をスロー
				response.EnsureSuccessStatusCode() ;
			
				// 4. 応答結果の文字列を読み取る
				string responseBody = await response.Content.ReadAsStringAsync() ;
			
				return responseBody ;
			}
			catch( HttpRequestException e )
			{
				// 接続エラーや非成功ステータスコード（4xx, 5xxなど）の場合の処理
				Console.WriteLine( $"\n例外発生 (HttpRequestException): {e.Message}" ) ;
				return null ;
			}
			catch( Exception ex )
			{
				// その他の予期せぬ例外の処理
				Console.WriteLine( $"\n予期せぬ例外: {ex.Message}" ) ;
				return null ;
			}
		}
	}

	//--------------------------------------------------------------------------------------------

	/// <summary>
	/// ファイルパスの取得・操作関係
	/// </summary>
	public class FilePath
	{
		/// <summary>
		/// 適切なカレンドディレクトリを取得する
		/// </summary>
		/// <returns></returns>
		public static string GetCurrentDirectory()
		{
			string path = Directory.GetCurrentDirectory().Replace( "\\", "/" ) ;

			if( Debugger.IsAttached == true )
			{
				// デバッガーから実行した

				if( path[ ^1 ] == '/' )
				{
					// 最後の一文字が / だったら削る
					path = path.Trim( '/' ) ;
				}

				string code ;
				int i, l ;

				l = path.Length ;
				i = path.LastIndexOf( '/' ) ;
				if( i >= 0 )
				{
					code = path[ ( i + 1 )..l ] ;
					if( code.IndexOf( "net" ) == 0 )
					{
						// 削って良い
						path = path[ ..i ] ;
					}
				}

				l = path.Length ;
				i = path.LastIndexOf( '/' ) ;
				if( i >= 0 )
				{
					code = path[ ( i + 1 )..l ] ;
					if( code.IndexOf( "Debug" ) == 0 || code.IndexOf( "Release" ) == 0 )
					{
						// 削って良い
						path = path[ ..i ] ;
					}
				}

				l = path.Length ;
				i = path.LastIndexOf( '/' ) ;
				if( i >= 0 )
				{
					code = path[ ( i + 1 )..l ] ;
					if( code.IndexOf( "bin" ) == 0 )
					{
						// 削って良い
						path = path[ ..i ] ;
					}
				}

				// 以下を実行するとソリューションフォルダになる
	//			i = path.LastIndexOf( '/' ) ;
	//			if( i >= 0 )
	//			{
	//				// 削って良い
	//				path = path[ ..i ] ;
	//			}

				// プロジェクトフォルダのパスが変えされる
				return path ;
			}
			else
			{
				return path ;
			}
		}

		/// <summary>
		/// 連結したパスを取得する
		/// </summary>
		/// <param name="upperPath"></param>
		/// <param name="lowerPath"></param>
		/// <returns></returns>
		public static string GetMergedPath( string upperPath, string lowerPath )
		{
			upperPath = upperPath.Replace( "\\", "/" ) ;
			lowerPath = lowerPath.Replace( "\\", "/" ) ;

			if( string.IsNullOrEmpty( upperPath ) == true )
			{
				return lowerPath ;
			}

			if( upperPath[ ^1 ] == '/' )
			{
				// 最後が / だったら削る
				upperPath = upperPath[ ..( upperPath.Length - 1 ) ] ;
			}

			if( lowerPath.IndexOf( "./" ) == 0 )
			{
				// 相対パス指定
				return upperPath + lowerPath[ 1.. ] ;
			}
			else
			if( lowerPath.IndexOf( "../" ) == 0 )
			{
				// 相対パス指定

				int i ;
				while( lowerPath.IndexOf( "../" ) == 0 && upperPath.LastIndexOf( '/' ) >= 0 )
				{
					lowerPath = lowerPath[ 3.. ] ;

					i = upperPath.LastIndexOf( '/' ) ;
					upperPath = upperPath[ ..i ] ;
				}

				return upperPath + "/" + lowerPath ;
			}
			else
			if( lowerPath.IndexOf( '/' ) == 0 )
			{
				return upperPath + lowerPath ;
			}
			else
			{
				return upperPath + "/" + lowerPath ;
			}
		}
	}


}	// namespace
