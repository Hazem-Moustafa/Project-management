using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Web;
using System.Web.SessionState;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using PMT.Controls;
using PMTComponents;
using PMTDataProvider;

namespace PMT.AllUsers
{
    public class Reports : Page
    {
        protected DropDownList ProjectDropDownList;
        protected Button ViewProjectButton;
        protected DropDownList ModuleDropDownList;
        protected Button ViewModuleButton;
        protected DropDownList TaskDropDownList;
        protected Button ViewTaskButton;
        protected Panel ReportPanel;
        protected string role;
        protected Report report;

        private void Page_Load(object sender, System.EventArgs e)
        {
            IDataProvider data = DataProviderFactory.CreateInstance();

            if (!this.IsPostBack)
            {
                if (UserRole.Equals(PMTUserRole.Manager))
                {
                    ProjectDropDownList.DataSource = data.GetProjects();
                    ProjectDropDownList.DataTextField="name";
                    ProjectDropDownList.DataValueField="ID";
                    ProjectDropDownList.DataBind();
                    ProjectDropDownList.Items.Insert(0,"");

                    enableModuleControls(false);
                    enableTaskControls(false);
                }
                else if (UserRole.Equals(PMTUserRole.Developer))
                {
                    // get all tasks assigned to a developer
                    TaskDropDownList.DataSource = data.GetDeveloperTasks(UserID);
                    TaskDropDownList.DataTextField="Name";
                    TaskDropDownList.DataValueField="ID";
                    TaskDropDownList.DataBind();
                    TaskDropDownList.Items.Insert(0,"");

                    enableModuleControls(false);
                    enableProjectControls(false);
                }
                else if (UserRole.Equals(PMTUserRole.Client))
                {
                    // get all projectes assigned to a client
                    ProjectDropDownList.DataSource = data.GetClientProjects(UserID);
                    ProjectDropDownList.DataTextField = "Name";
                    ProjectDropDownList.DataValueField = "ID";
                    ProjectDropDownList.DataBind();
                    ProjectDropDownList.Items.Insert(0,"");

                    ModuleDropDownList.Visible = false;
                    TaskDropDownList.Visible = false;
                }
            }

            ReportPanel.Visible = false;
        }

        #region Web Form Designer generated code
        override protected void OnInit(EventArgs e)
        {
            //
            // CODEGEN: This call is required by the ASP.NET Web Form Designer.
            //
            InitializeComponent();
            base.OnInit(e);
        }
		
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {    
            this.ProjectDropDownList.SelectedIndexChanged += new System.EventHandler(this.ProjectDropDownList_SelectedIndexChanged);
            this.ViewProjectButton.Click += new System.EventHandler(this.ViewReportButton_Click);
            this.ModuleDropDownList.SelectedIndexChanged += new System.EventHandler(this.ModuleDropDownList_SelectedIndexChanged);
            this.ViewModuleButton.Click += new System.EventHandler(this.ViewReportButton_Click);
            this.TaskDropDownList.SelectedIndexChanged += new System.EventHandler(this.TaskDropDownList_SelectedIndexChanged);
            this.ViewTaskButton.Click += new System.EventHandler(this.ViewReportButton_Click);
            this.Load += new System.EventHandler(this.Page_Load);

        }
        #endregion

        /// <summary>
        /// Handle View Report button clicks
        /// </summary>
        private void ViewReportButton_Click(object sender, System.EventArgs e)
        {
            string buttonID = ((Button)sender).ID;

            if (!Page.IsValid)
                return;

            IDataProvider data = DataProviderFactory.CreateInstance();

            if ( buttonID.Equals( "ViewProjectButton" ) )
            {
                Project project = data.GetProject(Convert.ToInt32(ProjectDropDownList.SelectedValue));

                report.Item = project;
                report.FillForm();
            }
            else if ( buttonID.Equals( "ViewModuleButton" ) )
            {
                Module module = data.GetModule(Convert.ToInt32(ModuleDropDownList.SelectedValue));

                report.Item = module;
                report.FillForm();
            }
            else //( buttonID.Equals( "ViewTaskButton" ) )
            {
                Task task = data.GetTask(Convert.ToInt32(TaskDropDownList.SelectedValue));

                report.Item = task;
                report.FillForm();
            }

            ReportPanel.Visible = true;
        }


        private void ProjectDropDownList_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (ProjectDropDownList.SelectedValue.Equals(String.Empty))
            {
                ViewProjectButton.Enabled = false;
                enableModuleControls(false);
                enableTaskControls(false);
                return;
            }

            IDataProvider data = DataProviderFactory.CreateInstance();
            ModuleDropDownList.DataSource = data.GetProjectModules(Convert.ToInt32(ProjectDropDownList.SelectedValue));
            ModuleDropDownList.DataTextField = "Name";
            ModuleDropDownList.DataValueField = "ID";
            ModuleDropDownList.DataBind();
            ModuleDropDownList.Items.Insert(0, String.Empty);

            enableProjectControls(true);
            enableModuleControls(true);
        }

        private void ModuleDropDownList_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (ModuleDropDownList.SelectedValue.Equals(String.Empty))
            {
                ViewModuleButton.Enabled = false;
                enableTaskControls(false);
                return;
            }

            IDataProvider data = DataProviderFactory.CreateInstance();
            TaskDropDownList.DataSource = data.GetModuleTasks(Convert.ToInt32(ModuleDropDownList.SelectedValue));
            TaskDropDownList.DataTextField = "Name";
            TaskDropDownList.DataValueField = "ID";
            TaskDropDownList.DataBind();
            TaskDropDownList.Items.Insert(0, String.Empty);

            enableModuleControls(true);
            enableTaskControls(true);
        }

        private void TaskDropDownList_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (TaskDropDownList.SelectedValue.Equals(String.Empty))
            {
                ViewTaskButton.Enabled = false;
                return;
            }

            enableTaskControls(true);
        }

        private void enableTaskControls(bool val)
        {
            TaskDropDownList.Enabled = val;

            if (TaskDropDownList.SelectedIndex > 0)
            {
                ViewTaskButton.Enabled = val;
            }
            else
            {
                ViewTaskButton.Enabled = false;
            }
        }

        private void enableModuleControls(bool val)
        {
            ModuleDropDownList.Enabled = val;

            if (ModuleDropDownList.SelectedIndex > 0)
            {
                ViewModuleButton.Enabled = val;
            }
            else
            {
                ViewModuleButton.Enabled = false;
            }
        }

        private void enableProjectControls(bool val)
        {
            ProjectDropDownList.Enabled = val;

            if(ProjectDropDownList.SelectedIndex > 0)
            {
                ViewProjectButton.Enabled = val;
            }
            else
            {
                ViewProjectButton.Enabled = false;
            }
        }

        #region Properties
        public int UserID
        {
            get {   return Convert.ToInt32(Request.Cookies["user"]["id"]);   }
        }
        public PMTUserRole UserRole
        {
            get {   return (PMTUserRole)Enum.Parse(typeof(PMTUserRole), Request.Cookies["user"]["role"]);   }
        }
        #endregion
    }
}