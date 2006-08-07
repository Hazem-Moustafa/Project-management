using System;
using System.Data;
using System.Text;
using MySql;
using MySql.Data;
using MySql.Data.MySqlClient;
using PMTComponents;

namespace PMTDataProvider
{
	/// <summary>
	/// MySql implementation of the PMT IDataProvider
	/// </summary>
    public class MySqlDataProvider : IDataProvider
    {
        public MySqlDataProvider() {}

        #region IDataProvider Members

        #region PMTUser
        public bool AuthenticateUser(string username, string password, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select count(*) from users where UserName=?user";
                command.Parameters.Add("?user", username);

                int k = Convert.ToInt32(this.ExecuteScalar(command));

                if (k == 0)
                {
                    //user does not exist in DB
                    handler(new Exception("You have entered an unknown username."));
                    return false;
                }
                else
                {
                    command = conn.CreateCommand();
                    command.CommandText = "select count(*) from users u where u.UserName=?user and u.Password=?pass";
                    command.Parameters.Add("?user", username);
                    command.Parameters.Add("?pass", password);

                    k = Convert.ToInt32(this.ExecuteScalar(command));
                    if (k == 0)
                    {
                        //password incorrect
                        handler(new Exception("You have entered an incorrect password."));
                        return false;
                    }
                    else
                    {
                        command = conn.CreateCommand();
                        command.CommandText = "select count(*) from users u where u.UserName=?user and u.Enabled=1";
                        command.Parameters.Add("?user", username);

                        k = Convert.ToInt32(this.ExecuteScalar(command));
                        if (k == 0)
                        {
                            //user not enabled
                            handler(new Exception("Your account has not been enabled.  Please contact your Administrator."));
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        #region PMTUser Management
        /// <summary>
        /// Enable a new user
        /// </summary>
        /// <param name="id">User id</param>
        public bool EnablePMTUser(int id, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                PMTUser user = this.GetPMTUserById(id, conn);

                if (user == null)
                {
                    handler(new NullReferenceException(String.Format("User with id {0} does not exist.", id)));
                    return false;
                }

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "update users set enabled=1 where id=?id";
                command.Parameters.Add("?id", id);

                try
                {
                    int rows = this.ExecuteNonQuery(command);
                    if (rows == 0)
                        return false;
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Disables a user
        /// </summary>
        /// <param name="id">User ID</param>
        public bool DisablePMTUser(int id, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                PMTUser user = this.GetPMTUserById(id, conn);

                if (user == null)
                {
                    handler(new NullReferenceException(String.Format("User with id {0} does not exist.", id)));
                    return false;
                }

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "update users set enabled=0 where id=?id";
                command.Parameters.Add("?id", id);

                try
                {
                    int rows = this.ExecuteNonQuery(command);
                    if (rows == 0)
                        return false;
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Delete a PMTUser
        /// </summary>
        /// <param name="id">User ID</param>
        public bool DeletePMTUser(int id, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "delete from users where id=?id";

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        public bool InsertPMTUser(PMTUser user, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                // add the user
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("insert into users (Username, Password, Role, Enabled) \n");
                sbCommand.Append("values (?user, ?password, ?role, ?enabled)");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?user", user.UserName);
                command.Parameters.Add("?password", user.Password);
                command.Parameters.Add("?role", (int)user.Role);
                command.Parameters.Add("?enabled", user.Enabled ? 1 : 0);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }

                // get the user we just inserted so we can have its ID
                PMTUser temp = this.GetPMTUserByUsername(user.UserName);
                if (temp == null)
                {
                    handler(new NullReferenceException("User could not be added."));
                    return false;
                }

                user.ID = temp.ID;

                // add the user's info
                sbCommand = new StringBuilder();
                sbCommand.Append("insert into userInfo (ID, FirstName, LastName, Address, City, State, Zip, PhoneNumber, Email) \n");
                sbCommand.Append("values (?id, ?firstName, ?lastName, ?address, ?city, ?state, ?zip, ?phone, ?email)");

                command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", user.ID);
                command.Parameters.Add("?firstName", user.FirstName);
                command.Parameters.Add("?lastName", user.LastName);
                command.Parameters.Add("?address", user.Address);
                command.Parameters.Add("?city", user.City);
                command.Parameters.Add("?state", user.State);
                command.Parameters.Add("?zip", user.ZipCode);
                command.Parameters.Add("?phone", user.PhoneNumber);
                command.Parameters.Add("?email", user.Email);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Update an existing user
        /// </summary>
        public bool UpdatePMTUser(PMTUser user, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                // update the user
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("update users  set Username=?user, Password=?password, Role=?role, Enabled=?enabled \n");
                sbCommand.Append("where ID=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?user", user.UserName);
                command.Parameters.Add("?password", user.Password);
                command.Parameters.Add("?role", (int)user.Role);
                command.Parameters.Add("?enabled", user.Enabled ? 1 : 0);
                command.Parameters.Add("?id", user.ID);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }

                // update the user's info
                sbCommand = new StringBuilder();
                sbCommand.Append("update userInfo \n");
                sbCommand.Append("set FirstName=?firstName, LastName=?lastName, Address=?address, City=?city, State=?state, Zip=?zip, PhoneNumber=?phone, Email=?email \n");
                sbCommand.Append("where ID=?id");

                command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", user.ID);
                command.Parameters.Add("?firstName", user.FirstName);
                command.Parameters.Add("?lastName", user.LastName);
                command.Parameters.Add("?address", user.Address);
                command.Parameters.Add("?city", user.City);
                command.Parameters.Add("?state", user.State);
                command.Parameters.Add("?zip", user.ZipCode);
                command.Parameters.Add("?phone", user.PhoneNumber);
                command.Parameters.Add("?email", user.Email);
                command.Parameters.Add("?id", user.ID);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }
        #endregion PMTUser Management

        #region Get Users
        /// <summary>
        /// Gets all users
        /// </summary>
        public DataTable GetPMTUsers()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from users u left join userInfo i on u.id=i.id";

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally
                {
                }
            }
            return dt;
        }

        /// <summary>
        /// Gets either enabled or disabled users
        /// </summary>
        /// <param name="enabled">Enabled or Disabled users?</param>
        public DataTable GetEnabledPMTUsers(bool enabled)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from users u left join userInfo i on u.id=i.id where u.Enabled=?enabled";
                command.Parameters.Add("?enabled", enabled ? 1 : 0);

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally
                {
                }
            }
            return dt;
        }
        #endregion Get Users

        #region Get PMTUser
        /// <summary>
        /// Gets a user by id
        /// </summary>
        /// <param name="id">User ID</param>
        public PMTUser GetPMTUserById(int id)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                return this.GetPMTUserById(id, conn);
            }
        }                

        /// <summary>
        /// Gets a user by id.  Internal method that reuses a connection
        /// </summary>
        /// <param name="id">User ID</param>
        private PMTUser GetPMTUserById(int id, MySqlConnection conn)
        {
            PMTUser user = null;
            MySqlCommand command = conn.CreateCommand();
            command.CommandText = "select * from users u left join userInfo i on u.id=i.id where u.id=?id";
            command.Parameters.Add("?id", id);

            if (conn.State != ConnectionState.Open)
                conn.Open();
            MySqlDataReader dr = command.ExecuteReader();

            while (dr.Read())
            {
                user = new PMTUser(
                    Convert.ToInt32(dr["id"]),
                    dr["userName"].ToString(),
                    dr["password"].ToString(),
                    (PMTUserRole)Convert.ToInt32(dr["role"]),
                    dr["firstName"].ToString(),
                    dr["lastName"].ToString(),
                    dr["email"].ToString(),
                    dr["phoneNumber"].ToString(),
                    dr["address"].ToString(),
                    dr["city"].ToString(),
                    dr["state"].ToString(),
                    dr["zip"].ToString(),
                    Convert.ToInt32(dr["enabled"]) == 1);
            }
            dr.Close();
            return user;
        }

        /// <summary>
        /// Returns a PMTUser by username
        /// </summary>
        public PMTUser GetPMTUserByUsername(string username)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();

                command.CommandText = "select ID from users where UserName=?user";
                command.Parameters.Add("?user", username);

                int id = Convert.ToInt32(this.ExecuteScalar(command));
                return this.GetPMTUserById(id, conn);
            }
        }
        #endregion Get PMTUser

        /// <summary>
        /// Is the email address in the userInfo table?
        /// </summary>
        /// <param name="email">Email Address</param>
        public bool VerifyEmailExists(string email)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select count(*) from userInfo where email=?email";
                command.Parameters.Add("?email", email);

                int k = Convert.ToInt32(this.ExecuteScalar(command));

                if (k > 0)
                    return true;
                else
                    return false;
            }
        }

        /*
         * This isn't needed, we can do a GetPMTUserByUsername(),
         *  and if the return is null, it does not exist
         * 
        /// <summary>
        /// Verify that a username exists in the database
        /// </summary>
        /// <param name="userName">the username to verify</param>
        /// <returns>true if it exists, false if it doesn't</returns>
        static public bool verifyUserNameExists(string userName, bool isNew)
        {
            DBDriver myDB=new DBDriver();
            myDB.Query="select count(*) from users where userName=@name;";
            myDB.addParam("@name", userName);
            int k=Convert.ToInt32(myDB.scalar());
            if(k!=1)
                if(isNew)
                {
                    myDB.Query="select count(*) from newUsers where userName=@name;";
                    myDB.addParam("@name", userName);
                    k=Convert.ToInt32(myDB.scalar());                                                                  
                }
            if(k==1)
                return true;

            return false;
        }
        */
        #endregion PMTUser

        #region C vs C Matrix
        public DataTable GetCompMatrix()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from compMatrix";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public bool UpdateCompMatrix(CompLevel level, double low, double med, double high, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "update compMatrix set lowComplexity=?low, medComplexity=?med, highComplexity=?high where compLevel=?level";
                command.Parameters.Add("?low", low);
                command.Parameters.Add("?med", med);
                command.Parameters.Add("?high", high);
                command.Parameters.Add("?level", (int)level);
                
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region Projects
        public DataTable GetProjects()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from projects";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public Project GetProject(int id)
        {
            Project project = null;
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from projects where id=?id";
                command.Parameters.Add("?id", id);
                
                MySqlDataReader dr = command.ExecuteReader();
                try
                {
                    while (dr.Read())
                    {
                        project = new Project(
                            Convert.ToInt32(dr["id"]),
                            Convert.ToInt32(dr["managerID"]),
                            dr["name"].ToString(),
                            dr["description"].ToString(),
                            Convert.ToDateTime(dr["startDate"]),
                            Convert.ToDateTime(dr["expEndDate"]),
                            Convert.ToDateTime(dr["actEndDate"]));
                    }
                }
                finally{}
            }
            return project;
        }

        public DataTable GetManagerProjects(int mgrID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select * from projects p left join userReference u on p.ID=u.projectID \n");
                sbCommand.Append("where u.managerID=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", mgrID);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public DataTable GetProjectModules(int projID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from modules where projectID=?id";
                command.Parameters.Add("?id", projID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);

                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public int InsertProject(Project project, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                // insert the project
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("insert into projects (name, description, startDate, expEndDate) \n");
                sbCommand.Append("values (?name, ?desc, ?start, ?expEnd)");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?name", project.Name);
                command.Parameters.Add("?desc", project.Description);
                command.Parameters.Add("?start", project.StartDate);
                command.Parameters.Add("?expEnd", project.ExpEndDate);
                
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }

                // get its id
                command = conn.CreateCommand();
                command.CommandText = "select LAST_INSERT_ID()";

                try
                {
                    id = Convert.ToInt32(this.ExecuteScalar(command));
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }

                // tie the project to its manager
                command = conn.CreateCommand();
                command.CommandText = "insert into userReference (userID, projectID, managerID) values (?uID, ?pID, ?mID)";
                command.Parameters.Add("?uID", project.ManagerID);
                command.Parameters.Add("?pID", id);
                command.Parameters.Add("?mID", project.ManagerID);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }
            }
            return id;
        }

        public bool UpdateProject(Project project, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("update projects set name=?name, description=?desc, startDate=?start, expEndDate=?expEnd, actEndDate=?actEnd \n");
                sbCommand.Append("where id=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", project.ID);
                command.Parameters.Add("?name", project.Name);
                command.Parameters.Add("?desc", project.Description);
                command.Parameters.Add("?start", project.StartDate);
                command.Parameters.Add("?expEnd", project.ExpEndDate);
                command.Parameters.Add("?actEnd", project.ActEndDate);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        public bool DeleteProject(int projID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("delete from projects where id=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", projID);
                
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }
        #endregion Projects

        #region Modules
        public DataTable GetModules()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from modules";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public PMTComponents.Module GetModule(int id)
        {
            PMTComponents.Module module = null;
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from modules where id=?id";
                command.Parameters.Add("?id", id);
                
                MySqlDataReader dr = command.ExecuteReader();
                try
                {
                    while (dr.Read())
                    {
                        module = new PMTComponents.Module(
                            Convert.ToInt32(dr["id"]),
                            Convert.ToInt32(dr["projectID"]),
                            dr["name"].ToString(),
                            dr["description"].ToString(),
                            Convert.ToDateTime(dr["startDate"]),
                            Convert.ToDateTime(dr["expEndDate"]),
                            Convert.ToDateTime(dr["actEndDate"]));
                    }
                }
                finally{}
            }
            return module;
        }

        public DataTable GetModuleTasks(int modID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from tasks where moduleID=?id";
                command.Parameters.Add("?id", modID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);

                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public int InsertModule(PMTComponents.Module module, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("insert into modules (projectID, name, description, startDate, expEndDate) \n");
                sbCommand.Append("values (?projID, ?name, ?desc, ?start, ?expEnd)");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?projID", module.ProjectID);
                command.Parameters.Add("?name", module.Name);
                command.Parameters.Add("?desc", module.Description);
                command.Parameters.Add("?start", module.StartDate);
                command.Parameters.Add("?expEnd", module.ExpEndDate);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }

                command = conn.CreateCommand();
                command.CommandText = "select LAST_INSERT_ID()";

                try
                {
                    id = Convert.ToInt32(this.ExecuteScalar(command));
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }
            }
            return id;
        }

        public bool UpdateModule(PMTComponents.Module module, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("update modules set projectID=?projID, name=?name, description=?desc, startDate=?start, expEndDate=?expEnd, actEndDate=?actEnd \n");
                sbCommand.Append("where id=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", module.ID);
                command.Parameters.Add("?projID", module.ProjectID);
                command.Parameters.Add("?name", module.Name);
                command.Parameters.Add("?desc", module.Description);
                command.Parameters.Add("?start", module.StartDate);
                command.Parameters.Add("?expEnd", module.ExpEndDate);
                command.Parameters.Add("?actEnd", module.ActEndDate);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        public bool DeleteModule(int modID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("delete from modules where id=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", modID);
                
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }
        #endregion Modules

        #region Tasks
        public DataTable GetTasks()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from tasks";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public Task GetTask(int id)
        {
            Task task = null;
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from tasks where id=?id";
                command.Parameters.Add("?id", id);
                
                MySqlDataReader dr = command.ExecuteReader();
                try
                {
                    while (dr.Read())
                    {
                        task = new Task(
                            Convert.ToInt32(dr["id"]),
                            Convert.ToInt32(dr["moduleID"]),
                            Convert.ToInt32(dr["projectID"]),
                            dr["name"].ToString(),
                            dr["description"].ToString(),
                            (TaskComplexity)Convert.ToInt32(dr["complexity"]),
                            Convert.ToDateTime(dr["startDate"]),
                            Convert.ToDateTime(dr["expEndDate"]),
                            Convert.ToDateTime(dr["actEndDate"]));
                    }
                }
                finally{}
            }
            return task;
        }

        public DataTable GetDeveloperTasks(int devID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select * from tasks t left join taskAssignments a on t.ID=t.taskID \n");
                sbCommand.Append("where u.devID=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", devID);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public int InsertTask(Task task, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("insert into tasks (moduleID, projectID, name, description, startDate, expEndDate) \n");
                sbCommand.Append("values (?modID, ?projID, ?name, ?desc, ?start, ?expEnd)");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?modID", task.ModuleID);
                command.Parameters.Add("?projID", task.ProjectID);
                command.Parameters.Add("?name", task.Name);
                command.Parameters.Add("?desc", task.Description);
                command.Parameters.Add("?start", task.StartDate);
                command.Parameters.Add("?expEnd", task.ExpEndDate);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }

                try
                {
                    id = Convert.ToInt32(this.ExecuteScalar(command));
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return id;
                }

            }
            return id;
        }

        public bool UpdateTask(Task task, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("update tasks set moduleID=?modID, projectID=?projID, name=?name, description=?desc, startDate=?start, expEndDate=?expEnd, actEndDate=?actEnd \n");
                sbCommand.Append("where id=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?modID", task.ModuleID);
                command.Parameters.Add("?projID", task.ProjectID);
                command.Parameters.Add("?id", task.ID);
                command.Parameters.Add("?name", task.Name);
                command.Parameters.Add("?desc", task.Description);
                command.Parameters.Add("?start", task.StartDate);
                command.Parameters.Add("?expEnd", task.ExpEndDate);
                command.Parameters.Add("?actEnd", task.ActEndDate);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        public bool DeleteTask(int taskID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "delete from tasks where id=?id";
                command.Parameters.Add("?id", taskID);
                
                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        public bool AssignDeveloper(int devID, int taskID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "insert into taskAssignments (devID, taskID) values (?devID, ?taskID)";
                command.Parameters.Add("?devID", devID);
                command.Parameters.Add("?taskID", taskID);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return false;
        }

        #endregion Tasks

        public DataTable GetDeveloperAssignments()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select * from (users u left join taskAssignments a on u.id=a.devID) \n");
                sbCommand.Append("left join tasks t on a.taskID=t.ID where u.Role=?role \n");
                sbCommand.Append("order by u.UserName");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?role", (int)PMTUserRole.Developer);

                MySqlDataAdapter da = new MySqlDataAdapter(command);

                try
                {
                    da.Fill(dt);
                }
                finally{}
            }
            return dt;
        }

        public bool ApproveTask(int taskID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Configuration.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "update tasks set Status=?status, actEndDate=?date \n";
                command.Parameters.Add("?status", (int)TaskStatus.Approved);
                command.Parameters.Add("?date", DateTime.Now);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }
            }
            return true;
        }

        public double ResolvePercentComplete(ProjectItem item)
        {
            /*
                int count = 0;
                int approved = 0;

                DataTable modsTable = this.getModulesDataSet().Tables[0];
                foreach (DataRow modRow in modsTable.Rows)
                {
                    DataTable tasksTable = new Module(modRow["id"].ToString()).getTasksDataSet().Tables[0];
                    foreach (DataRow taskRow in tasksTable.Rows)
                    {
                        if (taskRow["complete"].ToString().Equals(TaskStatus.APPROVED))
                            approved++;
                        count++;
                    }
                }
                double pct = (double)approved/(double)count;
                if (Double.IsNaN(pct))
                    pct = 0;
                return Convert.ToString(pct.ToString(percentFormatter));
            */

            /*
                int count = 0;
                int approved = 0;

                DataTable dt = this.getTasksDataSet().Tables[0];
                foreach (DataRow row in dt.Rows)
                {
                    if (row["complete"].ToString().Equals(TaskStatus.APPROVED))
                        approved++;
                    count++;
                }
                double pct = (double)approved/(double)count;
                if (Double.IsNaN(pct))
                    pct = 0;
                return Convert.ToString(pct.ToString(percentFormatter));
            */
            return 0;
        }

        public DateTime ResolveExpectedEndDate(ProjectItem item)
        {
            return DateTime.Now;
        }

        #region Managed Query Execution
        /// <summary>
        /// Execute a query that returns the number of rows affected, and not data
        /// </summary>
        private int ExecuteNonQuery(MySqlCommand command)
        {
            int rows = 0;

            if (command.Connection.State != ConnectionState.Open)
                command.Connection.Open();

            rows = command.ExecuteNonQuery();

            return rows;
        }

        /// <summary>
        /// Execute a command that returns an object
        /// </summary>
        private object ExecuteScalar(MySqlCommand command)
        {
            object obj = null;

            if (command.Connection.State != ConnectionState.Open)
                command.Connection.Open();

            obj = command.ExecuteScalar();
            
            return obj;
        }
        #endregion

        #endregion
    }
}
