const slides = document.querySelectorAll('.slide');
const nextBtn = document.querySelector('.arrow.right');
const prevBtn = document.querySelector('.arrow.left');

let index = 0;

function showSlide(i) {
  slides[index].classList.remove('active');
  index = (i + slides.length) % slides.length;
  slides[index].classList.add('active');
}

function nextSlide() {
  showSlide(index + 1);
}

function prevSlide() {
  showSlide(index - 1);
}

// Csak akkor fut, ha az oldalon tényleg vannak diák
if (slides.length > 1) {

  // Automatikus váltás
  let autoSlide = setInterval(nextSlide, 5000);

  // A nyilak a HTML-ben jelenleg nincsenek, de ha valaki hozzáadja őket,
  // működni fognak (és a kézi váltás újraindítja az időzítőt).
  function manualSlide(change) {
    change();
    clearInterval(autoSlide);
    autoSlide = setInterval(nextSlide, 5000);
  }

  if (nextBtn) nextBtn.addEventListener('click', () => manualSlide(nextSlide));
  if (prevBtn) prevBtn.addEventListener('click', () => manualSlide(prevSlide));
}
