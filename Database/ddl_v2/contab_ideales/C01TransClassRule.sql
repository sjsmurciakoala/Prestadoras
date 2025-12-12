USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01TransClassRule]    Script Date: 10/12/2025 13:48:15 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01TransClassRule](
	[ID_Class] [varchar](5) NOT NULL,
	[ID_Account1] [varchar](24) NULL,
	[ID_Account2] [varchar](24) NULL,
	[ID_Cost1] [varchar](8) NULL,
	[ID_Cost2] [varchar](8) NULL,
	[ID_Third1] [varchar](30) NULL,
	[ID_Third2] [varchar](30) NULL,
	[NLine] [int] IDENTITY(1,1) NOT NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [C01TransClassRule0] PRIMARY KEY CLUSTERED 
(
	[ID_Class] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01TransClassRule] ADD  DEFAULT ((1)) FOR [Status]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK01] FOREIGN KEY([ID_Class])
REFERENCES [dbo].[C01TransClass] ([ID_Class])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK01]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK02] FOREIGN KEY([ID_Account1])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK02]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK03] FOREIGN KEY([ID_Cost1])
REFERENCES [dbo].[C01CostCenter] ([ID_Cost])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK03]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK04] FOREIGN KEY([ID_Third1])
REFERENCES [dbo].[C01Thirds] ([ID_Third])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK04]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK05] FOREIGN KEY([ID_Account2])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK05]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK06] FOREIGN KEY([ID_Cost2])
REFERENCES [dbo].[C01CostCenter] ([ID_Cost])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK06]
GO

ALTER TABLE [dbo].[C01TransClassRule]  WITH CHECK ADD  CONSTRAINT [C01TransClassRuleFK07] FOREIGN KEY([ID_Third2])
REFERENCES [dbo].[C01Thirds] ([ID_Third])
GO

ALTER TABLE [dbo].[C01TransClassRule] CHECK CONSTRAINT [C01TransClassRuleFK07]
GO


