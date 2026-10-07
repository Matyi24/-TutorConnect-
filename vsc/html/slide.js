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

// Automatikus váltás
let autoSlide = setInterval(nextSlide, 5000);
