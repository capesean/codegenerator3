namespace WEB.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class preventSortHtml : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Entities", "PreventSortHtmlDeployment", c => c.String(maxLength: 100));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Entities", "PreventSortHtmlDeployment");
        }
    }
}
