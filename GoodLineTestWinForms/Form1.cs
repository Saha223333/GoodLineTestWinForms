using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GoodLineTestWinForms
{
    public partial class MainForm : Form
    {
        private SqlDataAdapter DataAdapterMerchandise;
        private DataSet MainDataSet;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            DB db = new DB();
            Exception ex = db.EstablishConnection(ServerNameTextBox.Text, DBNameTextBox.Text, LoginTextBox.Text, PasswordTextBox.Text);
            if (ex != null) MessageBox.Show("Ploho");
            MainDataSet = db.ReturnDataSet();

            FillMerchandiseGrid();

        }

        private void FillMerchandiseGrid()
        {
            MerchandiseDataGrid.AutoGenerateColumns = false;//выключаем автогенерацию столбцов грида (чтобы потом самостоятельно создать столбец с выпадающим списком)

            StringBuilder sb = new StringBuilder();
            sb.Append("Select * from dbo.Merchandise");//придумываем команду select

            DataAdapterMerchandise = new SqlDataAdapter(sb.ToString(), Properties.Settings.Default.ConnectionString);//создаём адаптер
            //описываем команды адаптера для связи с источником данных
            //команда обновления
            SqlCommand cmd = new SqlCommand("Update dbo.Merchandise set id_category = @id_category, name = @name, price = @price, dt = @dt where id = @old_id", DataAdapterMerchandise.SelectCommand.Connection);
            cmd.Parameters.Add("@id_category", SqlDbType.Int, 10, "id_category");//задаём параметры
            cmd.Parameters.Add("@name", SqlDbType.NVarChar, 150, "name");//задаём параметры
            cmd.Parameters.Add("@old_id", SqlDbType.Int, 10, "id");//этот параметр особый
            cmd.Parameters.Add("@price", SqlDbType.Money, 100, "price");//цена
            cmd.Parameters.Add("@dt", SqlDbType.DateTime, 100, "dt");//дата-время
            cmd.Parameters["@old_id"].SourceVersion = DataRowVersion.Original;
            DataAdapterMerchandise.UpdateCommand = cmd;
            //команда вставки
            cmd = new SqlCommand("Insert into dbo.Merchandise (id_category, name, price) values (@id_category, @name, @price)", DataAdapterMerchandise.SelectCommand.Connection);
            cmd.Parameters.Add("@id_category", SqlDbType.Int, 10, "id_category");//задаём параметры
            cmd.Parameters.Add("@name", SqlDbType.NVarChar, 150, "name");//задаём параметры
            cmd.Parameters.Add("@price", SqlDbType.Money, 100, "price");//цена
            DataAdapterMerchandise.InsertCommand = cmd;
            //команда удаления
            cmd = new SqlCommand("Delete from dbo.Merchandise where id = @id", DataAdapterMerchandise.SelectCommand.Connection);
            cmd.Parameters.Add("@id", SqlDbType.Int, 10, "id");//задаём параметры
            DataAdapterMerchandise.DeleteCommand = cmd;
            //мутим привязку таблицы к гриду
            BindingSource bs = new BindingSource();
            bs.DataSource = MainDataSet.Tables[0];
            MerchandiseDataGrid.DataSource = bs;
            //рисуем столбцы грида
            MerchandiseDataGrid.Columns[0].DataPropertyName = "name";//указываем для столбца конкретное поле таблицы (source)
            MerchandiseDataGrid.Columns[3].DataPropertyName = "price";
            MerchandiseDataGrid.Columns[2].DataPropertyName = "dt";
            //пытаемся создать lookup поле для столбца категорий товаров
            DataRelation r = new DataRelation("constraint", MainDataSet.Tables["Categories"].Columns["id"], MainDataSet.Tables["Merchandise"].Columns["id_category"]);//создаём объект связи таблиц по внешнему ключу
            DataGridViewComboBoxColumn dgvcbc = (DataGridViewComboBoxColumn)MerchandiseDataGrid.Columns[1];//чтобы задать иной источник данных для столбца нужно вручную создать экземпляр
            dgvcbc.DataSource = MainDataSet.Tables["Categories"];//источник данных столбца
            dgvcbc.DataPropertyName = "id_category";//поле внешнего ключа дочерней таблицы          
            dgvcbc.DisplayMember = "name";//поле родительской таблицы которое будет отображаться в выпадающем списке
            dgvcbc.ValueMember = "id";//первичный ключ родительской таблицы        

            CreateFilterTextBoxes(MerchandiseDataGrid);
        }

        private void SubmitButton_Click(object sender, EventArgs e)
        {
            try
            {
                DataAdapterMerchandise.Update(MainDataSet.Tables["Merchandise"]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void CreateFilterTextBoxes(DataGridView dgv)
        {//в этой процедуре создаём кастомные поля для ввода фильтра 
            dgv.Controls.Clear();
            BindingNavigator bn = new BindingNavigator(false);
            bn.GripStyle = ToolStripGripStyle.Hidden;//прячем ручку перетаскивания
            bn.CanOverflow = false;//запрещаем крайнему правому контролу прятаться 
            bn.Dock = DockStyle.None;//отвязываем якоря 
            bn.SetBounds(1, 21, dgv.Width, 0);//располагаем под заголовками столбцов
            dgv.Controls.Add(bn);
            //циклимся по столбцам грида
            foreach (DataGridViewColumn column in dgv.Columns)
            {             
                ToolStripTextBox textbox = new ToolStripTextBox();//создаём текстовое поле
                textbox.Tag = column;//помещаем в него столбец (для процедуры фильтрации)  
                column.Tag = textbox;//в столбец помещаем текстовое поле (для изменения ширины)
                textbox.AutoSize = false;
                textbox.Width = column.Width;
                textbox.TextChanged += new EventHandler(FilterApply);
                textbox.BackColor = Color.Beige;
                textbox.BorderStyle = BorderStyle.FixedSingle;
                textbox.Margin = new Padding(0);
                bn.Items.Add(textbox);//добавляем текстовое поле
            }
        }


        public string BuildQueryString(DataGridView dgv, DataTable lookuptable = null, string lookupfield = "")
        {
            StringBuilder QueryString = new StringBuilder("1=1");
            //составляем запрос на основании строковых полей поиска из грида
            //циклимся по столбцам грида
            foreach (DataGridViewColumn column in dgv.Columns)
            {
                ToolStripTextBox tstb = (ToolStripTextBox)column.Tag;

                if (column.ValueType == Type.GetType("System.String")) QueryString.Insert(QueryString.Length, " and " + column.Name + " like '%" + tstb.Text + "%'");//когда работаем со строковым столбцом
                if (column.ValueType == Type.GetType("System.DateTime")) //теперь случай с датой        
                {
                    if (tstb.Text.Length < 11)//если меньше 11 символов, то возвращаем всё
                        QueryString.Insert(QueryString.Length, " and " + column.Name + ">='01.01.1900'");
                    else///иначе есть смысл применять фильтр по дате                        
                    {
                        QueryString.Insert(QueryString.Length, " and " + column.Name + tstb.Text + "'");
                        QueryString.Insert(QueryString.Length - 11, "'");
                    }
                }
                //если lookup выпадающий список
                if (lookuptable != null & lookupfield != "" & column.ValueType == Type.GetType("System.Int32") & column.CellType.FullName == "System.Windows.Forms.DataGridViewComboBoxCell")
                {
                    //DataGridViewComboBoxColumn dgvcbc = (DataGridViewComboBoxColumn)column; //and id_category = (select id from categories where name = '')
                    lookuptable.DefaultView.RowFilter = lookupfield + " like '%" + tstb.Text + "%'";//фильтруем таблицу чтобы вытащить идентификатор нужного объекта
                    //нужно проциклиться по всем полученным строкам чтобы получить весь список подходящих идентификаторов
                    StringBuilder ids = new StringBuilder(" in (");              
                    DataTable filtered = lookuptable.DefaultView.ToTable();
                    foreach (DataRow row in filtered.Rows)
                    {                       
                        ids.Append(row["id"] + ", ");//накидываем идентификаторы в строку для предложения in
                    }
                    
                    ids.Remove(ids.Length - 2, 2);//удаляем последнюю лишнюю запятую
                    ids.Append(")");//добавляем завершающую скобку предложения in

                    QueryString.Insert(QueryString.Length, " and " + column.Name + ids.ToString());//непосрелственно формируем нужный запрос с участием идентификатора
                }
            }
            return QueryString.ToString();
        }


        public void FilterApply(object sender, EventArgs e)
        {
            ToolStripTextBox textbox = sender as ToolStripTextBox;
            BindingNavigator bn = (BindingNavigator)textbox.GetCurrentParent();
            DataGridView dgv = (DataGridView)bn.Parent;

            if (FilterByValue(dgv, BuildQueryString(dgv, MainDataSet.Tables["Categories"], "name")) == -1)//если ничего не нашли
            {
                return;
            }
        }

        public int FilterByValue(DataGridView dgv, string query)
        {//фильтр грида по указаннному запросу
            try
            {
                if (dgv.DataSource.GetType() == typeof(DataTable))
                {
                    DataTable dt = (DataTable)dgv.DataSource;
                    dt.DefaultView.RowFilter = query;
                }

                if (dgv.DataSource.GetType() == typeof(BindingSource))
                {
                    BindingSource bs = (BindingSource)dgv.DataSource;
                    bs.Filter = query;
                }

                dgv.Refresh();
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        private void MerchandiseDataGrid_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells["dt"].Value = DateTime.Now;
        }

        private void MerchandiseDataGrid_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (e.Column.Tag != null)
            {
                ToolStripTextBox tb = (ToolStripTextBox)e.Column.Tag;//получаем экземпляр поля для ввода
                tb.Width = e.Column.Width;
            }
            else
                return;
        }
    }
}