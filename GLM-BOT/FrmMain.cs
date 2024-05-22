using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Net;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;
using HZH_Controls;
using HZH_Controls.Forms;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using HZH_Controls.Controls;
using System.Threading;
using System.Web.UI.WebControls;

namespace GLM_BOT
{
    public partial class FrmMain : FrmWithTitle
    {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        string apikey = "";
        int max_tokens = 2048;
        string temperature = "";
        string top_p = "";
        int index = 0;//角色索引总数
        List<Message> messages = new List<Message>();
        Meta meta = new Meta();
        List<KeyValuePair<string, string>> characterList = new List<KeyValuePair<string, string>>();
        Dictionary<string, Meta> characters = new Dictionary<string, Meta>();
        public FrmMain()
        {
            InitializeComponent();
        }
        private void FrmMain_Load(object sender, EventArgs e)//窗体加载函数
        {
            ucTextBoxExAPI.PasswordChar = '*';//APIKEY输入窗口
            max_tokens = ucTrackBarMaxTokens.Value.ToInt();
            temperature = ucTrackBarTemp.Value.ToString();
            top_p = ucTrackBarTopp.Value.ToString();
            //读取本地key文件
            try
            {
                string key = @"key.txt";
                StreamReader re = new StreamReader(key);
                ucTextBoxExAPI.InputText = re.ReadLine();
                re.Close();
            }
            catch { }

            //将API窗口输入的值赋值给全局apiAddress变量
            apikey = ucTextBoxExAPI.InputText;

            //初始化各窗口右键菜单
            InitRichTextBoxContextMenu(richTextBoxQuestion, false);
            InitRichTextBoxContextMenu(richTextBoxAnswer, true);

            //初始化图片大小下拉菜单,默认关闭
            List<KeyValuePair<string, string>> picsize = new List<KeyValuePair<string, string>>();
            picsize.Add(new KeyValuePair<string, string>("0", "1024x1024"));
            ucComboxSize.Source = picsize;
            ucComboxSize.SelectedIndex = 0;
            ucComboxSize.Visible = false;

            //初始化模型下拉菜单
            List<KeyValuePair<string, string>> lstCom = new List<KeyValuePair<string, string>>();
            lstCom.Add(new KeyValuePair<string, string>("0", "glm-4"));
            lstCom.Add(new KeyValuePair<string, string>("1", "glm-3-turbo"));
            lstCom.Add(new KeyValuePair<string, string>("2", "charglm-3"));
            ucComboxModel.Source = lstCom;
            ucComboxModel.SelectedIndex = 0;
        }

        //GLM发送请求函数
        #region
        private async void GLMPicture(string str)//GLM绘图函数
        {
            string imageUrl = "";
            string model = "cogview-3"; // 模型名称
            string size = ucComboxSize.TextValue;
            var zhipuAI = new ZhipuAI();
            bool PictureIsOK = false; //是否正确生成图片标志位
            await zhipuAI.ImagesGenerations(apikey, model, str, size, (result) =>
            {
                if (!string.IsNullOrEmpty(result))
                {
                    JObject jsonData = JObject.Parse(result); //转为json
                    if (jsonData.ContainsKey("data"))
                    {
                        imageUrl = jsonData["data"][0]["url"].ToString();
                        PictureIsOK = true;
                    }
                    else
                    {
                        MessageBox.Show(jsonData["error"]["message"].ToString());
                    }
                }
            },cancellationTokenSource.Token);
            ucBtnImgStop.Visible = false;
            if (PictureIsOK)
            {
                try
                {
                    string pictureName = DateTime.Now.ToString("yyyyMMddhhmmss");

                    using (WebClient client = new WebClient())
                    {
                        byte[] imageData = await client.DownloadDataTaskAsync(imageUrl);
                        if (imageData != null && imageData.Length > 0)
                        {
                            using (var stream = new System.IO.MemoryStream(imageData))
                            {
                                Bitmap bitmap = new Bitmap(stream);

                                // 将图片保存到剪贴板
                                Clipboard.SetImage(bitmap);

                                // 在 UI 线程中执行
                                richTextBoxAnswer.BeginInvoke(new Action(() =>
                                {
                                    // 将图片粘贴到 RichTextBox 中
                                    richTextBoxAnswer.ReadOnly = false;
                                    richTextBoxAnswer.Paste();
                                    richTextBoxAnswer.ReadOnly = true;

                                    richTextBoxAnswer.AppendText($"\n---------------图片已保存到软件路径下，文件名\"{pictureName}.png\"---------------\n");
                                    richTextBoxQuestion.Focus();
                                }));

                                // 保存图片到本地
                                bitmap.Save($"{pictureName}.png", ImageFormat.Png);
                                bitmap.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"发生错误：{ex.Message}");
                }
            }
        }       
        private async void GLMSend(string str)//GLM非流式函数
        {
            string model = ucComboxModel.TextValue; // 模型名称
            bool stream = false; // 是否启用数据流
            messages.Add(new Message { role = "user", content = str });

            var zhipuAI = new ZhipuAI();
            await zhipuAI.ChatCompletionsCreate(apikey, model, max_tokens, temperature, top_p, messages, meta, stream, async (result) =>
            {
                if (!string.IsNullOrEmpty(result))
                {
                    JObject jsonData = JObject.Parse(result); //转为json
                    if (jsonData.ContainsKey("choices"))
                    {                       
                        string content = jsonData["choices"][0]["message"]["content"].ToString();

                        // 确保 richTextBoxAnswer 的操作在 UI 线程上执行
                        richTextBoxAnswer.Invoke((EventHandler)(delegate
                        {
                            richTextBoxAnswer.AppendText(content);
                            richTextBoxAnswer.AppendText("\n------------------------------------------------------------------------------------------------------------\n");
                            richTextBoxQuestion.Focus();
                        }));
                    }
                    else
                    {
                        MessageBox.Show(jsonData["error"]["message"].ToString());
                    }
                }
            }, cancellationTokenSource.Token);
            ucBtnImgStop.Visible = false;
        }     
        private async void GLMSendStream(string str)//GLM流式函数
        {
            string model = ucComboxModel.TextValue; // 模型名称
            bool stream = true; // 是否启用数据流
            messages.Add(new Message { role = "user", content = str });

            var zhipuAI = new ZhipuAI();
            await zhipuAI.ChatCompletionsCreate(apikey, model, max_tokens, temperature, top_p, messages, meta, stream, async (result) =>
            {
                if (!string.IsNullOrEmpty(result))
                {
                    if(!result.Contains("data: [DONE]"))
                    {
                        result = result.Replace("data:", "");//去掉开头的data：                                                             
                        JObject jsonData = JObject.Parse(result); //转为json
                        if(jsonData.ContainsKey("choices"))
                        {
                            string content = jsonData["choices"][0]["delta"]["content"].ToString();

                            // 使用异步等待延迟
                            await Task.Delay(5);

                            // 确保 richTextBoxAnswer 的操作在 UI 线程上执行
                            richTextBoxAnswer.Invoke((EventHandler)(delegate
                            {
                                richTextBoxAnswer.AppendText(content);
                            }));
                        }
                        else
                        {
                            MessageBox.Show(jsonData["error"]["message"].ToString());
                        }
                    }
                    else
                    {
                        // 确保 richTextBoxAnswer 的操作在 UI 线程上执行
                        richTextBoxAnswer.Invoke((EventHandler)(delegate
                        {
                            richTextBoxAnswer.AppendText("\n------------------------------------------------------------------------------------------------------------\n");
                            richTextBoxQuestion.Focus();
                        }));                      
                    }
                }
            }, cancellationTokenSource.Token);
            ucBtnImgStop.Visible = false;
        }
        private void ucBtnImgSend_BtnClick(object sender, EventArgs e)
        {
            cancellationTokenSource = new CancellationTokenSource();//创建一个新的 CancellationTokenSource以重置取消信号
            ucBtnImgStop.Visible = true;
            string question = richTextBoxQuestion.Text; //获取问题框中的文字作为问题
            if (ucCheckBoxPicture.Checked == false)
            {
                richTextBoxAnswer.AppendText("提问：" + question + "\n"); //将问题写入到答案框，并加上开头
                richTextBoxQuestion.Clear(); //清空问题框
                if (ucSwitchStream.Checked) //根据是否选择文字流来处理答案
                {
                    GLMSendStream(question); //流式GLM函数
                }
                else GLMSend(question); //非流式GLM函数
            }
            else
            {
                richTextBoxAnswer.AppendText("提示词：" + question + "\n" + "正在生成图片请稍后。。。" + "\n"); //将问题写入到答案框，并加上开头
                richTextBoxQuestion.Clear(); //清空问题框
                GLMPicture(question);
            }
        }
        private void ucBtnImgStop_BtnClick(object sender, EventArgs e)
        {
            cancellationTokenSource.Cancel();
            ucBtnImgStop.Visible = false;
            richTextBoxQuestion.Focus();
        }
        #endregion

        //界面函数
        #region
        private void ucTrackBarMaxTokens_ValueChanged(object sender, EventArgs e)
        {
            labelMaxTokens.Text = ucTrackBarMaxTokens.Value.ToString();
            max_tokens = ucTrackBarMaxTokens.Value.ToInt();
        }
        private void ucTrackBarTemp_ValueChanged(object sender, EventArgs e)
        {
            labelTemp.Text = ucTrackBarTemp.Value.ToString();
            temperature = ucTrackBarTemp.Value.ToString();
        }
        private void ucTrackBarTopp_ValueChanged(object sender, EventArgs e)
        {
            labelTopp.Text = ucTrackBarTopp.Value.ToString();
            top_p = ucTrackBarTopp.Value.ToString();
        }
        private void ucBtnImgClear_BtnClick(object sender, EventArgs e)
        {
            richTextBoxAnswer.Clear();
            messages.Clear();
        }
        private void richTextBoxAnswer_TextChanged(object sender, EventArgs e)
        {
            richTextBoxAnswer.SelectionStart = richTextBoxAnswer.Text.Length;
            richTextBoxAnswer.SelectionLength = 0;
            richTextBoxAnswer.Focus();
        }
        private void ucTextBoxExAPI_Leave(object sender, EventArgs e)
        {
            apikey = ucTextBoxExAPI.InputText;
            try
            {
                string key = @"key.txt";
                StreamWriter wr = new StreamWriter(key, false, Encoding.Default);
                wr.WriteLine(apikey);
                wr.Flush();
                wr.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("API地址更改失败，请检查是否有写入权限\n" + ex.Message);
            }
        }
        private void InitRichTextBoxContextMenu(RichTextBox textBox, bool read_only)
        {

            var contextMenu = new ContextMenu();
            //创建复制子菜单
            var copyMenuItem = new System.Windows.Forms.MenuItem("复制");
            copyMenuItem.Click += (sender, eventArgs) => textBox.Copy();
            contextMenu.MenuItems.Add(copyMenuItem);

            if (read_only == false)
            {
                //创建剪切子菜单
                var cutMenuItem = new System.Windows.Forms.MenuItem("剪切");
                cutMenuItem.Click += (sender, eventArgs) => textBox.Cut();


                //创建粘贴子菜单
                var pasteMenuItem = new System.Windows.Forms.MenuItem("粘贴");
                pasteMenuItem.Click += (sender, eventArgs) => textBox.Paste();

                //创建右键菜单并将子菜单加入到右键菜单中
                contextMenu.MenuItems.Add(cutMenuItem);
                contextMenu.MenuItems.Add(pasteMenuItem);
            }
            textBox.ContextMenu = contextMenu;
        }
        private void ucCheckBoxPicture_CheckedChangeEvent(object sender, EventArgs e)
        {
            ucComboxSize.Visible = ucCheckBoxPicture.Checked;
            ucComboxModel.Visible = !ucCheckBoxPicture.Checked;
            if(ucComboxModel.SelectedIndex == 2)
            {
                ucBtnImgAdd.Visible = ucBtnImgCharSetting.Visible = ucComboxCharacter.Visible = !ucCheckBoxPicture.Checked;
            }
        }
        private void richTextBoxQuestion_KeyDown(object sender, KeyEventArgs e)
        {
            // 检查是否按下了Enter键
            if (e.KeyCode == Keys.Enter && (e.Modifiers & Keys.Shift) != Keys.Shift)
            {
                // 阻止Enter键的默认行为（例如换行）
                e.SuppressKeyPress = true;

                // 手动调用点击事件
                ucBtnImgSend_BtnClick(null, new EventArgs());
            }
        }
        #endregion

        //角色扮演相关函数
        #region
        private void CharacterFresh() //刷新角色列表函数
        {
            int num = 0;//计算character.json中的角色值
            //初始化角色扮演下拉菜单
            characterList.Clear();
            characters.Clear();
            string filePath = "character.json";
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    characters = JsonConvert.DeserializeObject<Dictionary<string, Meta>>(json);

                    foreach (var item in characters)
                    {
                        // 使用序号作为Key，Name作为Value
                        characterList.Add(new KeyValuePair<string, string>(num.ToString(), item.Key));
                        num++;
                    }
                    index = num - 1;//将计算的角色数，赋值给全局的角色索引数
                    ucComboxCharacter.Source = characterList;
                    ucComboxCharacter.SelectedIndex = 0;
                    string selectedCharacterName = characterList[ucComboxCharacter.SelectedIndex].Value;
                    if (characters.TryGetValue(selectedCharacterName, out Meta selectedMeta))
                    {
                        meta = selectedMeta;
                    }
                }
                catch
                {
                    MessageBox.Show("character.json文件内容有误，请检查格式");
                }
            }
            else
            {
                MessageBox.Show("未找到character.json，请将文件放置在软件根目录");
            }
        }
        private void ucComboxModel_SelectedChangedEvent(object sender, EventArgs e)
        {
            if(ucComboxModel.SelectedIndex == 2)
            {
                ucBtnImgAdd.Visible = ucBtnImgCharSetting.Visible = ucComboxCharacter.Visible = true;
                CharacterFresh();
            }
            else
            {
                ucBtnImgAdd.Visible = ucBtnImgCharSetting.Visible = ucComboxCharacter.Visible = false;
            }
        }
        private void ucComboxCharacter_SelectedChangedEvent(object sender, EventArgs e)
        {
            string selectedCharacterName = characterList[ucComboxCharacter.SelectedIndex].Value;
            if (characters.TryGetValue(selectedCharacterName, out Meta selectedMeta))
            {
                meta = selectedMeta;
            }
        }
        private void ucBtnImgCharSetting_BtnClick(object sender, EventArgs e)
        {
            string name = ucComboxCharacter.TextValue;
            int SelectedIndex = ucComboxCharacter.SelectedIndex;
            FrmInputs frm = new FrmInputs(name,
            new string[] { "name","user_info", "bot_info", "bot_name", "user_name" },
            null,null,null,null,
            new Dictionary<string, string>() {
                { "name", name },
                { "user_info", meta.user_info }, 
                { "bot_info", meta.bot_info },
                { "bot_name", meta.bot_name },
                { "user_name", meta.user_name }
            }
            );
            frm.Size = new Size(775, 480);
            frm.RegionRadius = 40;
            
            // 显示窗体
            DialogResult result = frm.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                characters.Remove(name);
                if (frm.Values[0] != "")
                {
                    meta.user_info = frm.Values[1];
                    meta.bot_info = frm.Values[2];
                    meta.bot_name = frm.Values[3];
                    meta.user_name = frm.Values[4];
                    characters[frm.Values[0]] = meta;
                    characterList[SelectedIndex] = new KeyValuePair<string, string>(SelectedIndex.ToString(), frm.Values[0]);
                }
                else
                {
                    characterList.Remove(new KeyValuePair<string, string>(SelectedIndex.ToString(), name));
                    SelectedIndex--;
                }
                // 将更新后的字典序列化回 JSON 字符串
                string updatedJson = JsonConvert.SerializeObject(characters, Formatting.Indented);
                // 将 JSON 字符串写入文件
                File.WriteAllText("character.json", updatedJson);

                CharacterFresh();
                ucComboxCharacter.SelectedIndex = SelectedIndex;
                
            }
        }
        private void ucBtnImgAdd_BtnClick(object sender, EventArgs e)
        {
            int num = index++;//定义一个num变量用来对新增用户进行命名
            while(characters.ContainsKey($"新增{num}"))
            {
                num++;
            }
            characterList.Add(new KeyValuePair<string, string>(index.ToString(),$"新增{num}"));
            characters.Add($"新增{num}", new Meta());
            // 将更新后的字典序列化回 JSON 字符串
            string updatedJson = JsonConvert.SerializeObject(characters, Formatting.Indented);
            // 将 JSON 字符串写入文件
            File.WriteAllText("character.json", updatedJson);

            ucComboxCharacter.SelectedIndex = index;
            
        }
        #endregion
    }
}
