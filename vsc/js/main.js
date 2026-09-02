const express = require('express');
const app = express()
const path = require("path");

app.use("/css", express.static(path.join(__dirname, "../css")));
app.use("/js", express.static(path.join(__dirname, "../js")));
app.use("/html", express.static(path.join(__dirname, "../html")));

app.get('/', (req, res) => {
    res.send('hello world');
});



app.get("/index", (req, res) => {
    res.sendFile(path.join(__dirname, "../html/index.html"));
});
app.listen(3000, () => console.log('Listening on port 3000...'));