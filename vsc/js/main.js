const express = require('express');
const app = express()
const path = require("path");
const mysql = require("mysql2");

app.use("/css", express.static(path.join(__dirname, "../css")));
app.use("/js", express.static(path.join(__dirname, "../js")));
app.use("/html", express.static(path.join(__dirname, "../html")));


const connection = mysql.createConnection({
  host: "localhost",
  user: "root",
  password: "",
  database: "zsamo"
});


app.get("/", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/index.html"));
});

app.get("/index", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/index.html"));
});

app.get("/subs", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/subs.html"));
});

connection.connect((err) => {
  if (err) {
    console.error("Kapcsolódási hiba:", err);
    return;
  }

  console.log("Sikeresen csatlakozva a MySQL-hez!");
});



app.listen(3000, () => console.log('Listening on port 3000...'));  
