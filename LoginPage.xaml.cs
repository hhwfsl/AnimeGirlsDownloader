using Microsoft.UI.Xaml.Controls;
using System;

namespace AnimeGirlsDownloader
{
    public sealed partial class LoginPage : Page
    {
        private AccountAuth _accountAuth = new AccountAuth();
        private Action<string>? _afterLogin;
        public LoginPage()
        {
            InitializeComponent();
            _accountAuth.Initialize(ErrorHandler);
        }
        public void Initialize(Action<string> afterLogin)
        {
            _afterLogin = afterLogin;
        }
        private void ErrorHandler(string message)
        {
            LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageError_TeachingTip_Title");
            LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageError_TeachingTip_Subtitle");
            LoginPageTipTeachingTip.IsOpen = true;
        }
        private async void LoginPageLoginButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string userName = LoginAccountTextBox.Text;
            string password = LoginPasswordTextBox.Text;
            if(string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageTip_TeachingTip_Title");
                LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageTip_TeachingTip_Subtitle");
                LoginPageTipTeachingTip.IsOpen = true;
                return;
            }
            if (!await _accountAuth.Login(userName, password))
            {
                LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageError_TeachingTip_Title");
                LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageError_TeachingTip_Subtitle");
                LoginPageTipTeachingTip.IsOpen = true;
                return;
            }
            _afterLogin?.Invoke(userName);
            LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageSuccess_TeachingTip_Title");
            LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageSuccess_TeachingTip_Subtitle_Login");
            LoginPageTipTeachingTip.IsOpen = true;
            
        }

        private async void LoginPageRegistButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string userName = LoginAccountTextBox.Text;
            string password = LoginPasswordTextBox.Text;
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageTip_TeachingTip_Title");
                LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageTip_TeachingTip_Subtitle");
                LoginPageTipTeachingTip.IsOpen = true;
                return;
            }
            if (!await _accountAuth.Register(userName, password))
            {
                LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageError_TeachingTip_Title");
                LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageError_TeachingTip_Subtitle");
                LoginPageTipTeachingTip.IsOpen = true;
                return;
            }
            LoginPageTipTeachingTip.Title = AppResourceLoader.GetString("LoginPageSuccess_TeachingTip_Title");
            LoginPageTipTeachingTip.Subtitle = AppResourceLoader.GetString("LoginPageSuccess_TeachingTip_Subtitle_Register");
            LoginPageTipTeachingTip.IsOpen = true;
        }
    }
}
