<%@ Page Title="Benchmark History" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true"
    Async="True" CodeBehind="BenchmarkHistory.aspx.cs" Inherits="Nightly.BenchmarkHistory" %>

<%@ Register Assembly="AjaxControlToolkit" Namespace="AjaxControlToolkit" TagPrefix="asp" %>
<%@ Register Assembly="System.Web.DataVisualization, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" Namespace="System.Web.UI.DataVisualization.Charting" TagPrefix="asp" %>

<asp:Content ID="HeaderContent" ContentPlaceHolderID="HeadContent" runat="server" >
</asp:Content>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <asp:ScriptManager ID="ScriptManager1" runat="server" />

    <asp:PlaceHolder runat="server" ID="phTop" />

    <h1>Z3 Nightly Regression Test Benchmark History</h1>

    <br />

    <asp:PlaceHolder runat="server" ID="phHead">
    Benchmark filename: <asp:TextBox ID="txtFilename" runat="server" Enabled="true" Width="50%" />
    &nbsp; Days back: <asp:TextBox ID="txtDaysBack" runat="server" Enabled="true" Width="100" />
    &nbsp; <asp:Button ID="btnGo" runat="server" Text="Go!" />
    </asp:PlaceHolder>

    <br />
    <br />

    <asp:PlaceHolder runat="server" ID="phMain">
        <asp:Table ID="tblEntries" runat="server" BorderStyle="Solid" BorderWidth="1" HorizontalAlign="Center" Width="80%">
            <asp:TableHeaderRow BorderStyle="Solid" BackColor="Gray" ForeColor="Black">
                <asp:TableHeaderCell Width="5%" HorizontalAlign="Center" RowSpan="3">Job</asp:TableHeaderCell>
                <asp:TableHeaderCell Width="7%" HorizontalAlign="Center" RowSpan="3">Submission Time</asp:TableHeaderCell>
                <asp:TableHeaderCell HorizontalAlign="Center" ColumnSpan="6">Result</asp:TableHeaderCell>
            </asp:TableHeaderRow>
            <asp:TableHeaderRow BorderStyle="Solid" BackColor="Gray" ForeColor="Black">
                <asp:TableHeaderCell Width="5%" Font-Size="Smaller">Status</asp:TableHeaderCell>
                <asp:TableHeaderCell Width="5%" Font-Size="Smaller">Exit code</asp:TableHeaderCell>
                <asp:TableHeaderCell Width="5%" Font-Size="Smaller">CPU time</asp:TableHeaderCell>
                <asp:TableHeaderCell Width="5%" Font-Size="Smaller">Norm. CPU time</asp:TableHeaderCell>
                <asp:TableHeaderCell Width="5%" Font-Size="Smaller">Wall clock time</asp:TableHeaderCell>
                <asp:TableHeaderCell Width="5%" Font-Size="Smaller">Memory</asp:TableHeaderCell>
            </asp:TableHeaderRow>
            <asp:TableHeaderRow BorderStyle="Solid" BackColor="Gray" ForeColor="Black">
                <asp:TableHeaderCell Font-Size="Smaller"></asp:TableHeaderCell>
                <asp:TableHeaderCell Font-Size="Smaller"></asp:TableHeaderCell>
                <asp:TableHeaderCell Font-Size="Smaller">[sec]</asp:TableHeaderCell>
                <asp:TableHeaderCell Font-Size="Smaller">[sec]</asp:TableHeaderCell>
                <asp:TableHeaderCell Font-Size="Smaller">[sec]</asp:TableHeaderCell>
                <asp:TableHeaderCell Font-Size="Smaller">[MB]</asp:TableHeaderCell>
            </asp:TableHeaderRow>
        </asp:Table>
    </asp:PlaceHolder>

    <br />

    <div style="float: right; font-size: smaller; font-family: monospace; font-variant: small-caps;">
        Load time: <%= RenderTime.TotalSeconds %> sec.
    </div>
</asp:Content>