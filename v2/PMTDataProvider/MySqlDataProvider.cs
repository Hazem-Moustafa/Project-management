using System;
using System.Data;
using System.Text;
using System.Collections;
using MySql;
using MySql.Data;
using MySql.Data.MySqlClient;
using PMTComponents;
using PMTDataProvider.Configuration;

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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
        public bool EnablePMTUser(int id, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                PMTUser user = this.GetPMTUser(id, conn);

                if (user == null)
                {
                    handler(new NullReferenceException(String.Format("User with id {0} does not exist.", id)));
                    return false;
                }

                using (MySqlTransaction trans = conn.BeginTransaction())
                {
                    MySqlCommand command = conn.CreateCommand();

                    if (user.Role == PMTUserRole.Developer)
                    {
                        command.CommandText = "insert into compLevels (UserID, Competence) values (?id, ?comp)";
                        command.Parameters.Add("?id", user.ID);
                        command.Parameters.Add("?comp", (int)CompLevel.Low);

                        try
                        {
                            this.ExecuteNonQuery(command);
                        }
                        catch (MySqlException ex)
                        {
                            trans.Rollback();
                            handler(ex);
                            return false;
                        }
                        command.Parameters.Clear();
                    }

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
                        trans.Rollback();
                        handler(ex);
                        return false;
                    }
                    trans.Commit();
                }
            }
            return true;
        }

        public bool DisablePMTUser(int id, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                PMTUser user = this.GetPMTUser(id, conn);

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

        public bool DeletePMTUser(int id, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
                PMTUser temp = this.GetPMTUser(user.UserName);
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

        public bool UpdatePMTUser(PMTUser user, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                return UpdatePMTUser(conn, user, handler);
            }
        }

        private bool UpdatePMTUser(MySqlConnection conn, PMTUser user, TransactionFailedHandler handler)
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
            return true;
        }
        #endregion PMTUser Management

        #region Get Users
        public DataTable GetPMTUsers()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from users u left join userInfo i on u.id=i.id";

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }

        public DataTable GetEnabledPMTUsers(bool enabled)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from users u left join userInfo i on u.id=i.id where u.Enabled=?enabled";
                command.Parameters.Add("?enabled", enabled ? 1 : 0);

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }
        #endregion Get Users

        #region Get PMTUser
        public PMTUser GetPMTUser(int id)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                return this.GetPMTUser(id, conn);
            }
        }                

        private PMTUser GetPMTUser(int id, MySqlConnection conn)
        {
            PMTUser user = null;
            MySqlCommand command = conn.CreateCommand();
            command.CommandText = "select Username, u.ID as ID, Password, Role, Enabled, FirstName, LastName, Address, City, State, Zip, PhoneNumber, Email \n";
            command.CommandText += "from users u left join userInfo i on u.id=i.id where u.id=?id";
            command.Parameters.Add("?id", id);

            if (conn.State != ConnectionState.Open)
                conn.Open();
            MySqlDataReader dr = command.ExecuteReader();

            if (dr.Read())
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

        public PMTUser GetPMTUser(string username)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();

                command.CommandText = "select ID from users where UserName=?user";
                command.Parameters.Add("?user", username);

                int id = Convert.ToInt32(this.ExecuteScalar(command));
                return this.GetPMTUser(id, conn);
            }
        }

        public bool AssignPMTUser(int userID, int mgrID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                using (MySqlCommand comm = conn.CreateCommand())
                {
                    comm.CommandText = "insert into UserManagers (UserID, ManagerID) values (?user, ?mgr)";
                    comm.Parameters.Add("?user", userID);
                    comm.Parameters.Add("?mgr", mgrID);

                    try
                    {
                        this.ExecuteNonQuery(comm);
                    }
                    catch (MySqlException ex)
                    {
                        handler(ex);
                        return false;
                    }
                }
            }
            return true;
        }

        public bool UnassignPMTUser(int userID, int mgrID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                using (MySqlCommand comm = conn.CreateCommand())
                {
                    comm.CommandText = "delete from UserManagers where UserID=?user and ManagerID=?mgr";
                    comm.Parameters.Add("?user", userID);
                    comm.Parameters.Add("?mgr", mgrID);

                    try
                    {
                        this.ExecuteNonQuery(comm);
                    }
                    catch (MySqlException ex)
                    {
                        handler(ex);
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion Get PMTUser
        
        public bool VerifyEmailExists(string email)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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

        #region Developers
        /*
        public DataTable GetDevelopers()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("select u.id as ID, UserName, FirstName, LastName, Competence \n");
                sb.Append("from users u left join userinfo i on u.id=i.id left join complevels c on u.id=c.userID \n");
                sb.Append("where role=?role ");

                using (MySqlDataAdapter da = new MySqlDataAdapter(sb.ToString(), conn))
                {
                    da.SelectCommand.Parameters.Add("?role", (int)PMTUserRole.Developer);
                    da.Fill(dt);
                }
            }
            return dt;
        }
        */

        public DataTable GetDevelopers(int mgrID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("select Username, u.ID as ID, FirstName, LastName, Competence, sum(ManagerID=?mgrID) as Selected \n");
                sb.Append("from users u left join userinfo i on u.id=i.id left join usermanagers m on u.id=m.UserID left join complevels c on u.id=c.userID \n");
                sb.Append("where role=?role \n");
                sb.Append("group by ID, FirstName, LastName, Competence");

                using (MySqlDataAdapter da = new MySqlDataAdapter(sb.ToString(), conn))
                {
                    da.SelectCommand.Parameters.Add("?role", (int)PMTUserRole.Developer);
                    da.SelectCommand.Parameters.Add("?mgrID", mgrID);
                    da.Fill(dt);
                }
            }
            return dt;
        }

        public Developer GetDeveloper(int id)
        {
            Developer dev = null;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                // get the user
                PMTUser user = GetPMTUser(id);
                // if its not a developer, abort
                if (user.Role != PMTUserRole.Developer)
                    throw new Exception("User role must be Developer.");

                // make a developer
                dev = new Developer(user);

                using (MySqlCommand comm = conn.CreateCommand())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("select u.id as ID, Competence \n");
                    sb.Append("from users u left join complevels c on u.id=c.userID \n");
                    sb.Append("where role=?role and u.id=?id");
                    comm.CommandText = sb.ToString();
                    comm.Parameters.Add("?role", (int)PMTUserRole.Developer);
                    comm.Parameters.Add("?id", id);

                    conn.Open();
                    using (MySqlDataReader dr = comm.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            dev.Competency = (CompLevel)Convert.ToInt32(dr["Competence"]);
                        }
                    }
                }
            }
            return dev;
        }

        /// <summary>
        /// Needs Implemented
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        public bool UpdateDeveloper(Developer dev, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                using (MySqlTransaction trans = conn.BeginTransaction())
                {
                    throw new Exception("UpdateDeveloper not implemented");
                }
            }
        }
        #endregion
        #endregion PMTUser

        #region C vs C Matrix
        public DataTable GetCompMatrix()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from compMatrix";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }

        public bool UpdateCompMatrix(CompLevel level, double low, double med, double high, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from projects";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                da.Fill(dt);

            }
            return dt;
        }

        public Project GetProject(int userID, int projectID)
        {
            Project project = null;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("select ID, Name, Description, StartDate, ExpEndDate, ActEndDate, u.UserID as ManagerID \n");
                sb.Append("from projects p left join userProjects u on p.id=u.projectID \n");
                sb.Append("where ID=?pid \n");
                using (MySqlCommand command = conn.CreateCommand())
                {
                    command.CommandText = sb.ToString();
                    command.Parameters.Add("?pid", projectID);
                    command.Parameters.Add("?uid", userID);

                    conn.Open();
                    using (MySqlDataReader dr = command.ExecuteReader())
                    {
                        if (dr.Read())
                        {
                            project = new Project(
                                Convert.ToInt32(dr["ID"]),
                                Convert.ToInt32(dr["ManagerID"]),
                                dr["Name"].ToString(),
                                dr["Description"].ToString(),
                                Convert.ToDateTime(dr["StartDate"]),
                                Convert.ToDateTime(dr["ExpEndDate"]),
                                Convert.ToDateTime(dr["ActEndDate"]));
                        }
                    }
                }
            }
            return project;
        }

        public DataTable GetManagerProjects(int mgrID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select p.id as ID, Name, Description, StartDate, ExpEndDate, ActEndDate \n");
                sbCommand.Append("from projects p left join userProjects r on p.ID=r.projectID left join users u on u.id=r.userID \n");
                sbCommand.Append("where r.userID=?id and u.role=?role");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", mgrID);
                command.Parameters.Add("?role", (int)PMTUserRole.Manager);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                da.Fill(dt);
            }
            return dt;
        }

        public DataTable GetProjectModules(int projID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from modules where projectID=?id";
                command.Parameters.Add("?id", projID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);

                da.Fill(dt);
            }
            return dt;
        }

        public int InsertProject(Project project, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
                command.CommandText = "insert into userProjects (userID, projectID) values (?uID, ?pID)";
                command.Parameters.Add("?uID", project.ManagerID);
                command.Parameters.Add("?pID", id);

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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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

        /// <summary>
        /// IMPLIMENT ME !!!
        /// </summary>
        public DataTable GetClientProjects(int clientID)
        {
            return new DataTable();
        }
        #endregion Projects

        #region Modules
        public DataTable GetModules()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from modules";
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                da.Fill(dt);
            }
            return dt;
        }

        public PMTComponents.Module GetModule(int id)
        {
            PMTComponents.Module module = null;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from modules where id=?id";
                command.Parameters.Add("?id", id);
                
                conn.Open();
                MySqlDataReader dr = command.ExecuteReader();
                    if (dr.Read())
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
            return module;
        }

        public DataTable GetModuleTasks(int modID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from tasks where moduleID=?id";
                command.Parameters.Add("?id", modID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                da.Fill(dt);
            }
            return dt;
        }

        public int InsertModule(PMTComponents.Module module, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("select id, name, description, complexity, status, \n");
                sb.Append("startDate, expEndDate, actEndDate, \n");
                sb.Append("moduleID, projectID, devID, dateAssigned \n");
                sb.Append("from tasks t left join taskassignments a on t.id=a.taskID");

                using (MySqlDataAdapter da = new MySqlDataAdapter(sb.ToString(), conn))
                {
                    da.Fill(dt);
                }
            }
            return dt;
        }

        public Task GetTask(int id)
        {
            Task task = null;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from tasks where id=?id";
                command.Parameters.Add("?id", id);
                
                conn.Open();
                MySqlDataReader dr = command.ExecuteReader();
                    if (dr.Read())
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
            return task;
        }

        public DataTable GetDeveloperTasks(int devID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select * from tasks t left join taskAssignments a on t.ID=t.taskID \n");
                sbCommand.Append("where u.devID=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", devID);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                da.Fill(dt);
            }
            return dt;
        }

        public int InsertTask(Task task, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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

        public bool UpdateTask(Task task, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
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

        public bool UpdateTaskStatus(int taskID, TaskStatus status, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();

                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("update tasks set Status=?status ");
                if (status == TaskStatus.Approved)
                {
                    sbCommand.Append(", actEndDate=?date \n");
                    command.Parameters.Add("?date", DateTime.Now);
                }
                sbCommand.Append("where id=?id");

                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?status", (int)status);
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

        public bool AssignTask(int taskID, int devID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                conn.Open();
                using (MySqlTransaction trans = conn.BeginTransaction())
                {
                    using (MySqlCommand command = conn.CreateCommand())
                    {
                        // assign the task
                        command.CommandText = "insert into taskAssignments (devID, taskID, dateAssigned) values (?devID, ?taskID, ?date)";
                        command.Parameters.Add("?devID", devID);
                        command.Parameters.Add("?taskID", taskID);
                        command.Parameters.Add("?date", DateTime.Now);

                        try
                        {
                            this.ExecuteNonQuery(command);
                        }
                        catch (MySqlException ex)
                        {
                            trans.Rollback();
                            handler(ex);
                            return false;
                        }

                        // update the task status
                        command.CommandText = "update tasks set status=?status where id=?id";
                        command.Parameters.Clear();
                        command.Parameters.Add("?status", (int)TaskStatus.InProgress);
                        command.Parameters.Add("?id", taskID);

                        try
                        {
                            this.ExecuteNonQuery(command);
                        }
                        catch (MySqlException ex)
                        {
                            trans.Rollback();
                            handler(ex);
                            return false;
                        }
                    }
                    trans.Commit();
                }
            }
            return false;
        }

        public DataTable GetAvailableDevelopers(int numTasks)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select u.id as userID, u.userName as userName, count(t.id) as numTasks, c.competence as competence \n");
                sbCommand.Append("from users u inner join taskAssignments a on u.id=a.devID \n");
                sbCommand.Append("left join tasks t on a.taskID=t.ID \n");
                sbCommand.Append("left join compLevels c on a.devID=c.userID \n");
                sbCommand.Append("group by userID, userName, competence having numTasks<?numTasks");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?numTasks", numTasks);
                MySqlDataAdapter da = new MySqlDataAdapter(command);

                da.Fill(dt);
            }
            return dt;
        }

        public DataTable GetTaskAssignments(int mgrID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select u.id as userID, u.userName as userName, t.id as taskID, t.name as taskName, t.status as taskStatus, \n");
                sbCommand.Append("m.id as moduleID, m.name as moduleName, p.id as projectID, p.name as projectName, \n");
                sbCommand.Append("a.dateAssigned as assignDate, t.actEndDate as finishDate \n");
                sbCommand.Append("from users u inner join taskAssignments a on u.id=a.devID \n");
                sbCommand.Append("left join tasks t on a.taskID=t.ID \n");
                sbCommand.Append("left join modules m on t.moduleID=m.id \n");
                sbCommand.Append("left join projects p on t.projectID=p.id \n");
                sbCommand.Append("left join usermanagers g on u.id=g.userid \n");
                sbCommand.Append("where u.Role=?role and g.managerID=?mgrID \n");
                sbCommand.Append("order by u.UserName");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?role", (int)PMTUserRole.Developer);
                command.Parameters.Add("?mgrID", mgrID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }
        #endregion Tasks

        #region ProjectItem Common
        public double ResolvePercentComplete(ProjectItem item)
        {
            double percentComplete = 0;
            if (item is Project)
            {
                DataTable modules = GetProjectModules(item.ID);

                int count = 0;
                int completed = 0;
                foreach(DataRow modRow in modules.Rows)
                {
                    int id = Convert.ToInt32(modRow["id"]);
                    DataTable tasks = GetModuleTasks(id);

                    count += tasks.Rows.Count;
                    foreach(DataRow row in tasks.Rows)
                    {
                        TaskStatus status = (TaskStatus)Convert.ToInt32(row["Status"]);
                        if (status == TaskStatus.Approved)
                            completed ++;
                    }
                }

                percentComplete = (double)completed/(double)count; 
            }
            else if (item is Module)
            {
                DataTable tasks = GetModuleTasks(item.ID);

                int count = tasks.Rows.Count;
                int completed = 0;
                foreach(DataRow row in tasks.Rows)
                {
                    TaskStatus status = (TaskStatus)Convert.ToInt32(row["Status"]);
                    if (status == TaskStatus.Approved)
                        completed ++;
                }

                percentComplete = (double)completed/(double)count;
            }
            else if (item is Task)
            {
                TaskStatus status = (item as Task).Status;
                percentComplete = status == TaskStatus.Approved ? 1 : 0;
            }

            return Double.IsNaN(percentComplete) ? 0 : percentComplete;
        }

        /// <remarks>NEEDS IMPLIMENTED</remarks>
        public DateTime ResolveExpectedEndDate(ProjectItem item)
        {
            return DateTime.Now;
        }
        #endregion

        #region Messaging
        public DataTable GetSentMessages(int userID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from messages where senderID=?id";
                command.Parameters.Add("?id", userID);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                da.Fill(dt);
            }
            return dt;
        }

        public DataTable GetReceivedMessages(int userID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select m.id as messageID, u.username as senderName, r.dateReceived as date, m.subject as subject \n");
                sbCommand.Append("from recipients r left join messages m on r.messageID=m.id left join users u on u.id=r.recipientID \n");
                sbCommand.Append("where r.recipientID=?id");
                
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", userID);
                
                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }

        public Message GetMessage(int id)
        {
            Message m = null;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("select * from messages m left join recipients r on m.id=r.messageID \n");
                sbCommand.Append("where ID=?id");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?id", id);
                
                conn.Open();
                using (MySqlDataReader dr = command.ExecuteReader())
                {
                    if(dr.Read())
                    {
                        m = new Message();
                        m.ID = id;
                        m.Sender = GetPMTUser(Convert.ToInt32(dr["senderID"]));
                        m.DateSent = Convert.ToDateTime(dr["dateSent"]);
                        m.Subject = dr["subject"].ToString();
                        m.Body = dr["body"].ToString();
                    }
                }

                DataTable recipients = this.GetMessageRecipients(id);
                ArrayList users = new ArrayList();
                foreach (DataRow row in recipients.Rows)
                {
                    users.Add(GetPMTUser(Convert.ToInt32(row["recipientID"])));
                }

                m.Recipients = (PMTUser[]) users.ToArray(typeof(PMTUser));
            }
            return m;
        }

        private DataTable GetMessageRecipients(int messageID)
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select * from recipients where messageID=?id";
                command.Parameters.Add("?id", messageID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }

        public int InsertMessage(Message m, TransactionFailedHandler handler)
        {
            int id = -1;
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                // insert the message
                StringBuilder sbCommand = new StringBuilder();
                sbCommand.Append("insert into messages (senderID, dateSent, subject, body) \n");
                sbCommand.Append("values (?sender, ?sent, ?subject, ?body)");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                command.Parameters.Add("?sender", m.Sender.ID);
                command.Parameters.Add("?sent", m.DateSent);
                command.Parameters.Add("?subject", m.Subject);
                command.Parameters.Add("?body", m.Body);
                
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

                // get message id
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

                // add recipients
                sbCommand = new StringBuilder();
                sbCommand.Append("insert into recipients (messageID, recipientID) \n");
                sbCommand.Append("values (?messageID, ?recipientID)");

                command = conn.CreateCommand();
                command.CommandText = sbCommand.ToString();
                
                foreach(PMTUser user in m.Recipients)
                {
                    command.Parameters.Clear();
                    command.Parameters.Add("?messageID", id);
                    command.Parameters.Add("?recipientID", user.ID);

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
            }
            return id;
        }

        public bool DeleteMessage(int messageID, int recipientID, TransactionFailedHandler handler)
        {
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                // delete recipient's message
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "delete from recipients where messageID=?mID and recipientID=?rID";
                command.Parameters.Add("?mID", messageID);
                command.Parameters.Add("?rID", recipientID);

                try
                {
                    this.ExecuteNonQuery(command);
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }

                // get the number of recipients for this message
                command = conn.CreateCommand();
                command.CommandText = "select count(*) from recipients where messageID=?mID";
                command.Parameters.Add("?mID", messageID);

                int count = 0;
                try
                {
                    count = Convert.ToInt32(this.ExecuteScalar(command));
                }
                catch (MySqlException ex)
                {
                    handler(ex);
                    return false;
                }

                // if there are no recipients, delete the message
                if (count == 0)
                {
                    command.CommandText = "delete from messages where id=?mID";
                    
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
            }
            return true;
        }

        public DataTable GetContacts()
        {
            DataTable dt = new DataTable();
            using (MySqlConnection conn = new MySqlConnection(Config.ConnectionString))
            {
                MySqlCommand command = conn.CreateCommand();
                command.CommandText = "select u.id as id, u.username as username from users u "; /*left join userProjects p on u.id=p.userID where p.projectID=?pID";*/
                //command.Parameters.Add("?pID", projectID);

                MySqlDataAdapter da = new MySqlDataAdapter(command);
                    da.Fill(dt);
            }
            return dt;
        }
        #endregion

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
